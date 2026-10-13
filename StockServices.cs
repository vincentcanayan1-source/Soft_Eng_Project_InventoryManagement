namespace InventoryManagement;

/// <summary>Stock-in, stock-out, validation and transaction history (WBS tasks 7-10, 18).</summary>
public class StockService
{
    public const int MaxQuantityPerTransaction = 100_000;
    public const int MaxStockLevel = 1_000_000;

    private readonly Database _db;
    private readonly ProductService _products;
    private readonly AuthService _auth;

    public StockService(Database db, ProductService products, AuthService auth)
    {
        _db = db;
        _products = products;
        _auth = auth;
    }

    /// <summary>Receive goods from a supplier. Validated before any stock changes.</summary>
    public InventoryTransaction StockIn(int productId, int quantity, string reference)
    {
        _auth.RequireLogin();
        var product = _products.Get(productId);

        ValidateQuantity(quantity);
        reference = Guard.Text(reference, "Reference (delivery receipt or PO number)", 60);

        if (product.SupplierId == null)
            throw new InventoryException("Cannot receive stock: this product has no supplier assigned.");
        if ((long)product.Quantity + quantity > MaxStockLevel)
            throw new InventoryException($"Stock level cannot exceed {MaxStockLevel:N0} units.");

        var duplicate = _db.Data.Transactions.Any(t =>
            t.ProductId == productId &&
            t.Type == TransactionType.StockIn &&
            t.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
            throw new InventoryException($"Reference '{reference}' was already received for this product.");

        return Apply(product, TransactionType.StockIn, quantity, reference);
    }

    /// <summary>Issue stock (sale, usage, damage...). Never allows negative stock.</summary>
    public InventoryTransaction StockOut(int productId, int quantity, string reason)
    {
        _auth.RequireLogin();
        var product = _products.Get(productId);

        ValidateQuantity(quantity);
        reason = string.IsNullOrWhiteSpace(reason) ? "Issued" : Guard.Text(reason, "Reason", 60);

        if (quantity > product.Quantity)
            throw new InventoryException(
                $"Insufficient stock for {product.Sku}: requested {quantity}, only {product.Quantity} available.");

        return Apply(product, TransactionType.StockOut, quantity, reason);
    }

    /// <summary>Newest first. From/To are inclusive calendar dates.</summary>
    public List<InventoryTransaction> History(int? productId = null, TransactionType? type = null,
                                              DateTime? from = null, DateTime? to = null)
    {
        IEnumerable<InventoryTransaction> query = _db.Data.Transactions;
        if (productId.HasValue) query = query.Where(t => t.ProductId == productId.Value);
        if (type.HasValue) query = query.Where(t => t.Type == type.Value);
        if (from.HasValue) query = query.Where(t => t.Timestamp >= from.Value.Date);
        if (to.HasValue) query = query.Where(t => t.Timestamp < to.Value.Date.AddDays(1));
        return query.OrderByDescending(t => t.Id).ToList();
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new InventoryException("Quantity must be a whole number greater than zero.");
        if (quantity > MaxQuantityPerTransaction)
            throw new InventoryException($"Quantity per transaction cannot exceed {MaxQuantityPerTransaction:N0}.");
    }

    private InventoryTransaction Apply(Product product, TransactionType type, int quantity, string reference)
    {
        product.Quantity += type == TransactionType.StockIn ? quantity : -quantity;

        var transaction = new InventoryTransaction
        {
            Id = _db.Data.NextTransactionId++,
            Timestamp = DateTime.Now,
            ProductId = product.Id,
            Type = type,
            Quantity = quantity,
            BalanceAfter = product.Quantity,
            Reference = reference,
            PerformedBy = _auth.Actor
        };
        _db.Data.Transactions.Add(transaction);
        _db.Save();
        return transaction;
    }
}

public record Recommendation(Product Product, int SuggestedQuantity, string SupplierName, bool AlreadyOnOrder);

/// <summary>Reorder level monitoring and recommendations (WBS tasks 11-12).</summary>
public class ReorderService
{
    private readonly Database _db;
    private readonly SupplierService _suppliers;

    public ReorderService(Database db, SupplierService suppliers)
    {
        _db = db;
        _suppliers = suppliers;
    }

    public static bool IsLow(Product p) => p.ReorderLevel > 0 && p.Quantity <= p.ReorderLevel;

    /// <summary>"OUT", "LOW" or "OK".</summary>
    public static string StatusOf(Product p)
    {
        if (p.Quantity == 0) return "OUT";
        return IsLow(p) ? "LOW" : "OK";
    }

    public List<Product> LowStock() =>
        _db.Data.Products.Where(IsLow).OrderBy(p => p.Quantity).ThenBy(p => p.Name).ToList();

    /// <summary>
    /// Suggested order = enough to bring stock to twice the reorder level,
    /// but never less than the product's standard reorder quantity.
    /// </summary>
    public static int SuggestQuantity(Product p) =>
        Math.Max(1, Math.Max(p.ReorderQuantity, p.ReorderLevel * 2 - p.Quantity));

    public List<Recommendation> Recommend()
    {
        return LowStock().Select(p => new Recommendation(
            p,
            SuggestQuantity(p),
            _suppliers.NameOf(p.SupplierId),
            HasOpenOrder(p.Id))).ToList();
    }

    private bool HasOpenOrder(int productId) =>
        _db.Data.PurchaseOrders.Any(o => o.ProductId == productId &&
            (o.Status == PurchaseStatus.Pending || o.Status == PurchaseStatus.Ordered));
}

/// <summary>Purchase / restocking workflow (WBS task 13): Pending -> Ordered -> Received (or Cancelled).</summary>
public class PurchaseService
{
    private readonly Database _db;
    private readonly ProductService _products;
    private readonly StockService _stock;
    private readonly ReorderService _reorder;
    private readonly AuthService _auth;

    public PurchaseService(Database db, ProductService products, StockService stock,
                           ReorderService reorder, AuthService auth)
    {
        _db = db;
        _products = products;
        _stock = stock;
        _reorder = reorder;
        _auth = auth;
    }

    public PurchaseOrder Create(int productId, int quantity)
    {
        _auth.RequireLogin();
        var product = _products.Get(productId);

        if (quantity <= 0) throw new InventoryException("Order quantity must be greater than zero.");
        if (quantity > StockService.MaxQuantityPerTransaction)
            throw new InventoryException($"Order quantity cannot exceed {StockService.MaxQuantityPerTransaction:N0}.");
        if (product.SupplierId == null)
            throw new InventoryException("Cannot order: this product has no supplier assigned.");
        if (_db.Data.PurchaseOrders.Any(o => o.ProductId == productId &&
                (o.Status == PurchaseStatus.Pending || o.Status == PurchaseStatus.Ordered)))
            throw new InventoryException("This product already has an open purchase order.");

        var order = new PurchaseOrder
        {
            Id = _db.Data.NextPurchaseOrderId++,
            CreatedAt = DateTime.Now,
            ProductId = productId,
            SupplierId = product.SupplierId.Value,
            Quantity = quantity,
            Status = PurchaseStatus.Pending,
            CreatedBy = _auth.Actor
        };
        _db.Data.PurchaseOrders.Add(order);
        _db.Save();
        return order;
    }

    /// <summary>Creates a pending order for every recommendation that has a supplier and no open order.</summary>
    public List<PurchaseOrder> CreateFromRecommendations()
    {
        _auth.RequireLogin();
        var created = new List<PurchaseOrder>();
        foreach (var rec in _reorder.Recommend())
        {
            if (rec.AlreadyOnOrder || rec.Product.SupplierId == null) continue;
            created.Add(Create(rec.Product.Id, rec.SuggestedQuantity));
        }
        return created;
    }

    public void MarkOrdered(int orderId)
    {
        _auth.RequireAdmin();
        var order = Get(orderId);
        if (order.Status != PurchaseStatus.Pending)
            throw new InventoryException($"Only Pending orders can be marked as Ordered (this one is {order.Status}).");
        order.Status = PurchaseStatus.Ordered;
        _db.Save();
    }

    /// <summary>Receiving an order adds its quantity to stock through the normal stock-in rules.</summary>
    public void Receive(int orderId)
    {
        _auth.RequireLogin();
        var order = Get(orderId);
        if (order.Status != PurchaseStatus.Ordered)
            throw new InventoryException($"Only Ordered orders can be received (this one is {order.Status}).");

        _stock.StockIn(order.ProductId, order.Quantity, $"PO-{order.Id}");
        order.Status = PurchaseStatus.Received;
        order.ReceivedAt = DateTime.Now;
        _db.Save();
    }

    public void Cancel(int orderId)
    {
        _auth.RequireAdmin();
        var order = Get(orderId);
        if (order.Status != PurchaseStatus.Pending && order.Status != PurchaseStatus.Ordered)
            throw new InventoryException($"A {order.Status} order cannot be cancelled.");
        order.Status = PurchaseStatus.Cancelled;
        _db.Save();
    }

    public PurchaseOrder Get(int id) =>
        _db.Data.PurchaseOrders.FirstOrDefault(o => o.Id == id)
        ?? throw new InventoryException($"Purchase order #{id} was not found.");

    public List<PurchaseOrder> List(PurchaseStatus? status = null) =>
        _db.Data.PurchaseOrders.Where(o => status == null || o.Status == status).OrderByDescending(o => o.Id).ToList();
}
