using System.Globalization;
using System.Text;

namespace InventoryManagement;

/// <summary>A table of rows that can be shown on screen or saved as CSV.</summary>
public class Report
{
    public string Title { get; set; } = "";
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<string[]> Rows { get; set; } = new();

    public string ToText() => TextTable.Render(Headers, Rows);

    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Headers.Select(Escape)));
        foreach (var row in Rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}

public static class TextTable
{
    public static string Render(string[] headers, IEnumerable<string[]> rows)
    {
        var all = rows.ToList();
        var widths = headers.Select(h => h.Length).ToArray();
        foreach (var row in all)
            for (var i = 0; i < widths.Length && i < row.Length; i++)
                widths[i] = Math.Max(widths[i], row[i].Length);

        string Line(string[] cells) =>
            string.Join("  ", cells.Select((c, i) => c.PadRight(widths[i])).ToArray()).TrimEnd();

        var sb = new StringBuilder();
        sb.AppendLine(Line(headers));
        sb.AppendLine(string.Join("  ", widths.Select(w => new string('-', w)).ToArray()));
        foreach (var row in all) sb.AppendLine(Line(row));
        if (all.Count == 0) sb.AppendLine("(no records)");
        return sb.ToString().TrimEnd();
    }
}

public class DashboardData
{
    public int ProductCount { get; set; }
    public int SupplierCount { get; set; }
    public int TotalUnits { get; set; }
    public decimal TotalValue { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public int OpenPurchaseOrders { get; set; }
    public List<InventoryTransaction> RecentTransactions { get; set; } = new();
    public List<Product> TopByValue { get; set; } = new();
}

/// <summary>Inventory reports (WBS task 14) and dashboard figures (task 15).</summary>
public class ReportService
{
    private readonly Database _db;
    private readonly SupplierService _suppliers;

    public ReportService(Database db, SupplierService suppliers)
    {
        _db = db;
        _suppliers = suppliers;
    }

    public static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    public DashboardData Dashboard()
    {
        var d = _db.Data;
        return new DashboardData
        {
            ProductCount = d.Products.Count,
            SupplierCount = d.Suppliers.Count,
            TotalUnits = d.Products.Sum(p => p.Quantity),
            TotalValue = d.Products.Sum(p => p.StockValue),
            LowStockCount = d.Products.Count(ReorderService.IsLow),
            OutOfStockCount = d.Products.Count(p => p.Quantity == 0),
            OpenPurchaseOrders = d.PurchaseOrders.Count(o =>
                o.Status == PurchaseStatus.Pending || o.Status == PurchaseStatus.Ordered),
            RecentTransactions = d.Transactions.OrderByDescending(t => t.Id).Take(5).ToList(),
            TopByValue = d.Products.OrderByDescending(p => p.StockValue).Take(5).ToList()
        };
    }

    public Report StockLevels()
    {
        return new Report
        {
            Title = "Current Stock Levels",
            Headers = new[] { "SKU", "Product", "Category", "Qty", "Reorder Lvl", "Status", "Unit Price", "Value", "Supplier" },
            Rows = _db.Data.Products.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p => new[]
            {
                p.Sku, p.Name, p.Category, p.Quantity.ToString(), p.ReorderLevel.ToString(),
                ReorderService.StatusOf(p), Money(p.UnitPrice), Money(p.StockValue), _suppliers.NameOf(p.SupplierId)
            }).ToList()
        };
    }

    public Report LowStock()
    {
        return new Report
        {
            Title = "Low Stock / Reorder Report",
            Headers = new[] { "SKU", "Product", "Qty", "Reorder Lvl", "Status", "Supplier" },
            Rows = _db.Data.Products.Where(ReorderService.IsLow).OrderBy(p => p.Quantity).Select(p => new[]
            {
                p.Sku, p.Name, p.Quantity.ToString(), p.ReorderLevel.ToString(),
                ReorderService.StatusOf(p), _suppliers.NameOf(p.SupplierId)
            }).ToList()
        };
    }

    public Report Valuation()
    {
        var groups = _db.Data.Products
            .GroupBy(p => p.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = groups.Select(g => new[]
        {
            g.Key, g.Count().ToString(), g.Sum(p => p.Quantity).ToString(), Money(g.Sum(p => p.StockValue))
        }).ToList();
        rows.Add(new[]
        {
            "TOTAL", _db.Data.Products.Count.ToString(),
            _db.Data.Products.Sum(p => p.Quantity).ToString(), Money(_db.Data.Products.Sum(p => p.StockValue))
        });

        return new Report
        {
            Title = "Inventory Valuation by Category",
            Headers = new[] { "Category", "Products", "Units", "Value" },
            Rows = rows
        };
    }

    public Report TransactionSummary(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);
        var inRange = _db.Data.Transactions.Where(t => t.Timestamp >= start && t.Timestamp < end).ToList();

        var rows = new List<string[]>();
        foreach (var p in _db.Data.Products.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            var received = inRange.Where(t => t.ProductId == p.Id && t.Type == TransactionType.StockIn).Sum(t => t.Quantity);
            var issued = inRange.Where(t => t.ProductId == p.Id && t.Type == TransactionType.StockOut).Sum(t => t.Quantity);
            if (received == 0 && issued == 0) continue;
            rows.Add(new[] { p.Sku, p.Name, received.ToString(), issued.ToString(), (received - issued).ToString() });
        }

        return new Report
        {
            Title = $"Stock Movement {from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
            Headers = new[] { "SKU", "Product", "Stock In", "Stock Out", "Net" },
            Rows = rows
        };
    }

    public Report TransactionLog(IEnumerable<InventoryTransaction> transactions)
    {
        var products = _db.Data.Products.ToDictionary(p => p.Id);
        return new Report
        {
            Title = "Transaction History",
            Headers = new[] { "ID", "Date/Time", "SKU", "Type", "Qty", "Balance", "Reference", "By" },
            Rows = transactions.Select(t => new[]
            {
                t.Id.ToString(), t.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                products.TryGetValue(t.ProductId, out var p) ? p.Sku : "(deleted)",
                t.Type == TransactionType.StockIn ? "IN" : "OUT",
                t.Quantity.ToString(), t.BalanceAfter.ToString(), t.Reference, t.PerformedBy
            }).ToList()
        };
    }

    public Report PurchaseOrders(IEnumerable<PurchaseOrder> orders)
    {
        var products = _db.Data.Products.ToDictionary(p => p.Id);
        return new Report
        {
            Title = "Purchase Orders",
            Headers = new[] { "PO", "Created", "SKU", "Product", "Supplier", "Qty", "Status", "By" },
            Rows = orders.Select(o => new[]
            {
                o.Id.ToString(), o.CreatedAt.ToString("yyyy-MM-dd"),
                products.TryGetValue(o.ProductId, out var p) ? p.Sku : "(deleted)",
                p != null ? p.Name : "(deleted)",
                _suppliers.NameOf(o.SupplierId), o.Quantity.ToString(), o.Status.ToString(), o.CreatedBy
            }).ToList()
        };
    }

    /// <summary>Saves a report as a CSV file inside the given folder and returns the full path.</summary>
    public string ExportCsv(Report report, string directory)
    {
        Directory.CreateDirectory(directory);
        var slug = new string(report.Title.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var path = Path.Combine(directory, $"{slug}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllText(path, report.ToCsv(), new UTF8Encoding(true));
        return Path.GetFullPath(path);
    }
}
