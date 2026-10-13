namespace InventoryManagement;

/// <summary>Wires every service together (WBS tasks 1 and 17: set up and integrate all modules).</summary>
public class InventoryApp
{
    public Database Db { get; }
    public AuthService Auth { get; }
    public ProductService Products { get; }
    public SupplierService Suppliers { get; }
    public StockService Stock { get; }
    public ReorderService Reorder { get; }
    public PurchaseService Purchases { get; }
    public ReportService Reports { get; }

    public string ReportsDirectory { get; }

    public InventoryApp(string dataFilePath)
    {
        Db = new Database(dataFilePath);
        Auth = new AuthService(Db);
        Products = new ProductService(Db, Auth);
        Suppliers = new SupplierService(Db, Auth);
        Stock = new StockService(Db, Products, Auth);
        Reorder = new ReorderService(Db, Suppliers);
        Purchases = new PurchaseService(Db, Products, Stock, Reorder, Auth);
        Reports = new ReportService(Db, Suppliers);

        ReportsDirectory = Path.Combine(Path.GetDirectoryName(Db.FilePath) ?? ".", "reports");
        Auth.EnsureDefaultAdmin();
    }
}

/// <summary>Optional demo data, loaded with the --demo switch. Only runs on an empty database.</summary>
public static class SampleData
{
    public static void Load(InventoryApp app)
    {
        var data = app.Db.Data;
        if (data.Products.Count > 0 || data.Suppliers.Count > 0) return;

        int AddSupplier(string name, string contact, string phone, string email)
        {
            var s = new Supplier { Id = data.NextSupplierId++, Name = name, ContactPerson = contact, Phone = phone, Email = email };
            data.Suppliers.Add(s);
            return s.Id;
        }

        var techSupply = AddSupplier("TechSource Trading", "Maria Santos", "0917-555-0101", "sales@techsource.example");
        var officeHub = AddSupplier("OfficeHub Supplies", "John Reyes", "0918-555-0202", "orders@officehub.example");
        app.Db.Save();

        var admin = data.Users.First(u => u.Role == Role.Admin);
        app.Auth.RunAs(admin, () =>
        {
            app.Products.Add("KB-001", "USB Keyboard", "Peripherals", 450m, 10, 30, techSupply);
            app.Products.Add("MS-001", "Optical Mouse", "Peripherals", 250m, 10, 30, techSupply);
            app.Products.Add("MON-24", "24-inch Monitor", "Displays", 5800m, 3, 6, techSupply);
            app.Products.Add("PPR-A4", "A4 Bond Paper (ream)", "Office Supplies", 260m, 20, 50, officeHub);
            app.Products.Add("PEN-BL", "Ballpen Blue (box)", "Office Supplies", 95m, 15, 40, officeHub);

            app.Stock.StockIn(app.Products.FindBySku("KB-001")!.Id, 40, "DR-1001");
            app.Stock.StockIn(app.Products.FindBySku("MS-001")!.Id, 8, "DR-1002");
            app.Stock.StockIn(app.Products.FindBySku("MON-24")!.Id, 10, "DR-1003");
            app.Stock.StockIn(app.Products.FindBySku("PPR-A4")!.Id, 100, "DR-1004");
            app.Stock.StockIn(app.Products.FindBySku("PEN-BL")!.Id, 30, "DR-1005");
            app.Stock.StockOut(app.Products.FindBySku("PPR-A4")!.Id, 85, "Office issue");
            app.Stock.StockOut(app.Products.FindBySku("KB-001")!.Id, 5, "Sold");
        });
    }
}
