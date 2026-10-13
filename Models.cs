using System.Text.Json.Serialization;

namespace InventoryManagement;

public enum Role { Admin, Staff }
public enum TransactionType { StockIn, StockOut }
public enum PurchaseStatus { Pending, Ordered, Received, Cancelled }

/// <summary>Raised for any validation / business-rule problem. The UI shows the message to the user.</summary>
public class InventoryException : Exception
{
    public InventoryException(string message) : base(message) { }
}

public class User
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public Role Role { get; set; } = Role.Staff;
}

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "General";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int ReorderLevel { get; set; }
    public int ReorderQuantity { get; set; }
    public int? SupplierId { get; set; }

    [JsonIgnore]
    public decimal StockValue => UnitPrice * Quantity;
}

public class InventoryTransaction
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public int ProductId { get; set; }
    public TransactionType Type { get; set; }
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public string Reference { get; set; } = "";
    public string PerformedBy { get; set; } = "";
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ProductId { get; set; }
    public int SupplierId { get; set; }
    public int Quantity { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
    public string CreatedBy { get; set; } = "";
    public DateTime? ReceivedAt { get; set; }
}

/// <summary>Everything that is saved to disk.</summary>
public class DataStore
{
    public List<User> Users { get; set; } = new();
    public List<Supplier> Suppliers { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public List<InventoryTransaction> Transactions { get; set; } = new();
    public List<PurchaseOrder> PurchaseOrders { get; set; } = new();

    public int NextSupplierId { get; set; } = 1;
    public int NextProductId { get; set; } = 1;
    public int NextTransactionId { get; set; } = 1;
    public int NextPurchaseOrderId { get; set; } = 1;
}

/// <summary>Small input-validation helpers shared by the services.</summary>
public static class Guard
{
    public static string Text(string? value, string field, int maxLength = 100)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0) throw new InventoryException($"{field} is required.");
        if (text.Length > maxLength) throw new InventoryException($"{field} must be at most {maxLength} characters.");
        return text;
    }

    public static string OptionalText(string? value, string field, int maxLength = 100)
    {
        var text = (value ?? "").Trim();
        if (text.Length > maxLength) throw new InventoryException($"{field} must be at most {maxLength} characters.");
        return text;
    }

    public static void NonNegative(int value, string field)
    {
        if (value < 0) throw new InventoryException($"{field} cannot be negative.");
    }

    public static void NonNegative(decimal value, string field)
    {
        if (value < 0) throw new InventoryException($"{field} cannot be negative.");
    }
}
