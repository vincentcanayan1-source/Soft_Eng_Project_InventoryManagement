namespace InventoryManagement;

/// <summary>Product registration, search and listing (WBS tasks 3, 4, 6).</summary>
public class ProductService
{
    private readonly Database _db;
    private readonly AuthService _auth;

    public ProductService(Database db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public Product Add(string sku, string name, string category, decimal unitPrice,
                       int reorderLevel, int reorderQuantity, int? supplierId = null)
    {
        _auth.RequireLogin();
        var data = _db.Data;

        sku = Guard.Text(sku, "SKU", 30).ToUpperInvariant();
        if (sku.Contains(' ')) throw new InventoryException("SKU must not contain spaces.");
        name = Guard.Text(name, "Product name");
        category = string.IsNullOrWhiteSpace(category) ? "General" : Guard.Text(category, "Category", 50);
        Guard.NonNegative(unitPrice, "Unit price");
        Guard.NonNegative(reorderLevel, "Reorder level");
        Guard.NonNegative(reorderQuantity, "Reorder quantity");

        if (data.Products.Any(p => p.Sku == sku))
            throw new InventoryException($"SKU '{sku}' already exists.");
        if (supplierId.HasValue)
            EnsureSupplierExists(supplierId.Value);

        var product = new Product
        {
            Id = data.NextProductId++,
            Sku = sku,
            Name = name,
            Category = category,
            UnitPrice = unitPrice,
            ReorderLevel = reorderLevel,
            ReorderQuantity = reorderQuantity,
            SupplierId = supplierId,
            Quantity = 0
        };
        data.Products.Add(product);
        _db.Save();
        return product;
    }

    public void Update(int id, string name, string category, decimal unitPrice, int reorderLevel, int reorderQuantity)
    {
        _auth.RequireAdmin();
        var product = Get(id);

        name = Guard.Text(name, "Product name");
        category = string.IsNullOrWhiteSpace(category) ? "General" : Guard.Text(category, "Category", 50);
        Guard.NonNegative(unitPrice, "Unit price");
        Guard.NonNegative(reorderLevel, "Reorder level");
        Guard.NonNegative(reorderQuantity, "Reorder quantity");

        product.Name = name;
        product.Category = category;
        product.UnitPrice = unitPrice;
        product.ReorderLevel = reorderLevel;
        product.ReorderQuantity = reorderQuantity;
        _db.Save();
    }

    public void Delete(int id)
    {
        _auth.RequireAdmin();
        var product = Get(id);
        if (product.Quantity > 0)
            throw new InventoryException("Cannot delete a product that still has stock on hand.");
        if (_db.Data.Transactions.Any(t => t.ProductId == id) || _db.Data.PurchaseOrders.Any(o => o.ProductId == id))
            throw new InventoryException("Cannot delete a product that has transaction or purchase history.");

        _db.Data.Products.Remove(product);
        _db.Save();
    }

    /// <summary>Links a product to its supplier (or unlinks it when supplierId is null).</summary>
    public void AssignSupplier(int productId, int? supplierId)
    {
        _auth.RequireAdmin();
        var product = Get(productId);
        if (supplierId.HasValue) EnsureSupplierExists(supplierId.Value);
        product.SupplierId = supplierId;
        _db.Save();
    }

    public Product? Find(int id) => _db.Data.Products.FirstOrDefault(p => p.Id == id);

    public Product? FindBySku(string sku) =>
        _db.Data.Products.FirstOrDefault(p => p.Sku.Equals((sku ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

    public Product Get(int id) => Find(id) ?? throw new InventoryException($"Product #{id} was not found.");

    /// <summary>Accepts either a numeric ID or a SKU.</summary>
    public Product Resolve(string idOrSku)
    {
        var text = (idOrSku ?? "").Trim();
        if (int.TryParse(text, out var id))
        {
            var byId = Find(id);
            if (byId != null) return byId;
        }
        return FindBySku(text) ?? throw new InventoryException($"No product matches '{text}'.");
    }

    public List<Product> List() => _db.Data.Products.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public List<Product> Search(string? term, string? category = null, int? supplierId = null)
    {
        IEnumerable<Product> query = _db.Data.Products;

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            query = query.Where(p =>
                p.Sku.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
        if (supplierId.HasValue)
            query = query.Where(p => p.SupplierId == supplierId.Value);

        return query.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public List<string> Categories() =>
        _db.Data.Products.Select(p => p.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();

    private void EnsureSupplierExists(int supplierId)
    {
        if (!_db.Data.Suppliers.Any(s => s.Id == supplierId))
            throw new InventoryException($"Supplier #{supplierId} was not found.");
    }
}

/// <summary>Supplier management (WBS task 5).</summary>
public class SupplierService
{
    private readonly Database _db;
    private readonly AuthService _auth;

    public SupplierService(Database db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public Supplier Add(string name, string contactPerson, string phone, string email)
    {
        _auth.RequireAdmin();
        name = Guard.Text(name, "Supplier name");
        contactPerson = Guard.OptionalText(contactPerson, "Contact person");
        phone = Guard.OptionalText(phone, "Phone", 30);
        email = Guard.OptionalText(email, "Email");
        ValidateEmail(email);

        if (_db.Data.Suppliers.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InventoryException($"A supplier named '{name}' already exists.");

        var supplier = new Supplier
        {
            Id = _db.Data.NextSupplierId++,
            Name = name,
            ContactPerson = contactPerson,
            Phone = phone,
            Email = email
        };
        _db.Data.Suppliers.Add(supplier);
        _db.Save();
        return supplier;
    }

    public void Update(int id, string name, string contactPerson, string phone, string email)
    {
        _auth.RequireAdmin();
        var supplier = Get(id);
        name = Guard.Text(name, "Supplier name");
        contactPerson = Guard.OptionalText(contactPerson, "Contact person");
        phone = Guard.OptionalText(phone, "Phone", 30);
        email = Guard.OptionalText(email, "Email");
        ValidateEmail(email);

        if (_db.Data.Suppliers.Any(s => s.Id != id && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InventoryException($"A supplier named '{name}' already exists.");

        supplier.Name = name;
        supplier.ContactPerson = contactPerson;
        supplier.Phone = phone;
        supplier.Email = email;
        _db.Save();
    }

    public void Delete(int id)
    {
        _auth.RequireAdmin();
        var supplier = Get(id);
        if (_db.Data.Products.Any(p => p.SupplierId == id))
            throw new InventoryException("Cannot delete a supplier that is still linked to products.");
        if (_db.Data.PurchaseOrders.Any(o => o.SupplierId == id))
            throw new InventoryException("Cannot delete a supplier that has purchase orders.");

        _db.Data.Suppliers.Remove(supplier);
        _db.Save();
    }

    public Supplier? Find(int id) => _db.Data.Suppliers.FirstOrDefault(s => s.Id == id);

    public Supplier Get(int id) => Find(id) ?? throw new InventoryException($"Supplier #{id} was not found.");

    public string NameOf(int? id) => id.HasValue ? (Find(id.Value)?.Name ?? "(unknown)") : "(none)";

    public List<Supplier> List() => _db.Data.Suppliers.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public List<Product> ProductsOf(int supplierId) =>
        _db.Data.Products.Where(p => p.SupplierId == supplierId).OrderBy(p => p.Name).ToList();

    private static void ValidateEmail(string email)
    {
        if (email.Length > 0 && (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@')))
            throw new InventoryException("Email address is not valid.");
    }
}
