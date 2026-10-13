namespace InventoryManagement;

/// <summary>
/// Built-in tests (WBS tasks 19-22). Run with:  dotnet run -- --selftest
/// Uses a temporary data folder, so your real data is never touched.
/// </summary>
public static class SelfTest
{
    private static int _passed;
    private static int _failed;

    public static int Run()
    {
        _passed = 0;
        _failed = 0;
        var folder = Path.Combine(Path.GetTempPath(), "ims_selftest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var app = new InventoryApp(Path.Combine(folder, "inventory.json"));
            RunAll(app);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { /* ignore cleanup problems */ }
        }

        Console.WriteLine();
        Console.WriteLine($"Passed: {_passed}   Failed: {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static void RunAll(InventoryApp app)
    {
        Console.WriteLine("== Authentication and roles ==");
        Check("wrong password is rejected", !app.Auth.Login("admin", "nope"));
        Check("default admin can log in", app.Auth.Login("admin", "admin123"));
        app.Auth.AddUser("staff1", "secret1", Role.Staff);
        Throws("short password is rejected", () => app.Auth.AddUser("x1234", "abc", Role.Staff));
        Throws("duplicate username is rejected", () => app.Auth.AddUser("STAFF1", "secret1", Role.Staff));

        Console.WriteLine("== Suppliers and products ==");
        var supplier = app.Suppliers.Add("Test Supplier", "Ann", "123", "ann@test.example");
        Throws("duplicate supplier name is rejected", () => app.Suppliers.Add("test supplier", "", "", ""));
        Throws("bad email is rejected", () => app.Suppliers.Add("Other", "", "", "not-an-email"));

        var widget = app.Products.Add("wid-1", "Widget", "Parts", 10m, 5, 20, supplier.Id);
        Check("SKU is stored upper-case", widget.Sku == "WID-1");
        Throws("duplicate SKU is rejected", () => app.Products.Add("WID-1", "Again", "Parts", 1m, 0, 0));
        Throws("negative price is rejected", () => app.Products.Add("NEG-1", "Neg", "Parts", -1m, 0, 0));
        Throws("unknown supplier is rejected", () => app.Products.Add("BAD-1", "Bad", "Parts", 1m, 0, 0, 999));
        var loose = app.Products.Add("LOOSE-1", "No Supplier", "Parts", 5m, 2, 10);
        Check("search by name finds product", app.Products.Search("widg").Count == 1);
        Check("search by category finds both", app.Products.Search(null, "parts").Count == 2);
        Check("resolve by SKU works", app.Products.Resolve("wid-1").Id == widget.Id);
        Throws("cannot delete supplier linked to products", () => app.Suppliers.Delete(supplier.Id));

        Console.WriteLine("== Stock in / out ==");
        Throws("stock-in needs a supplier", () => app.Stock.StockIn(loose.Id, 5, "DR-X"));
        Throws("stock-in rejects zero quantity", () => app.Stock.StockIn(widget.Id, 0, "DR-0"));
        Throws("stock-in needs a reference", () => app.Stock.StockIn(widget.Id, 5, "  "));
        app.Stock.StockIn(widget.Id, 10, "DR-1");
        Check("stock-in adds quantity", widget.Quantity == 10);
        Throws("same reference cannot be received twice", () => app.Stock.StockIn(widget.Id, 1, "dr-1"));
        app.Stock.StockOut(widget.Id, 4, "Sold");
        Check("stock-out subtracts quantity", widget.Quantity == 6);
        Throws("stock-out cannot go negative", () => app.Stock.StockOut(widget.Id, 7, "Too many"));
        Check("failed stock-out leaves stock unchanged", widget.Quantity == 6);
        Check("history has 2 entries for the product", app.Stock.History(widget.Id).Count == 2);
        Check("history filter by type works", app.Stock.History(widget.Id, TransactionType.StockOut).Count == 1);
        Check("history today range works", app.Stock.History(null, null, DateTime.Today, DateTime.Today).Count == 2);
        Check("history balance is recorded", app.Stock.History(widget.Id)[0].BalanceAfter == 6);

        Console.WriteLine("== Reorder and purchasing ==");
        app.Stock.StockOut(widget.Id, 1, "Sold");   // 5 left = reorder level of 5
        Check("product at reorder level is low", ReorderService.IsLow(widget));
        Check("low-stock list contains it", app.Reorder.LowStock().Any(p => p.Id == widget.Id));
        var rec = app.Reorder.Recommend().First(r => r.Product.Id == widget.Id);
        Check("recommendation is at least the standard quantity", rec.SuggestedQuantity >= 20);

        var order = app.Purchases.Create(widget.Id, rec.SuggestedQuantity);
        Check("new order is Pending", order.Status == PurchaseStatus.Pending);
        Throws("second open order for same product is rejected", () => app.Purchases.Create(widget.Id, 5));
        Throws("pending order cannot be received", () => app.Purchases.Receive(order.Id));
        app.Purchases.MarkOrdered(order.Id);
        var before = widget.Quantity;
        app.Purchases.Receive(order.Id);
        Check("receiving an order adds stock", widget.Quantity == before + order.Quantity);
        Check("order becomes Received", app.Purchases.Get(order.Id).Status == PurchaseStatus.Received);
        Throws("received order cannot be cancelled", () => app.Purchases.Cancel(order.Id));

        Console.WriteLine("== Reports and dashboard ==");
        var dash = app.Reports.Dashboard();
        Check("dashboard counts products", dash.ProductCount == 2);
        Check("dashboard value matches", dash.TotalValue == widget.Quantity * 10m);
        var csv = app.Reports.StockLevels().ToCsv();
        Check("CSV has header and rows", csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 3);
        Check("valuation has a TOTAL row", app.Reports.Valuation().Rows.Last()[0] == "TOTAL");
        var exported = app.Reports.ExportCsv(app.Reports.LowStock(), app.ReportsDirectory);
        Check("CSV export creates a file", File.Exists(exported));

        Console.WriteLine("== Backup and recovery ==");
        var backup = app.Db.Backup();
        Check("backup file exists", File.Exists(backup));
        var quantityAtBackup = widget.Quantity;
        app.Stock.StockOut(widget.Id, 1, "After backup");
        app.Db.Restore(backup);
        Check("restore brings back the old quantity", app.Products.Get(widget.Id).Quantity == quantityAtBackup);
        Check("user is still valid after restore", app.Auth.Refresh());

        Console.WriteLine("== Data is saved to disk ==");
        var reopened = new InventoryApp(app.Db.FilePath);
        Check("products survive a restart", reopened.Products.List().Count == 2);
        Check("passwords survive a restart", reopened.Auth.Login("staff1", "secret1"));

        Console.WriteLine("== Role restrictions (logged in as staff) ==");
        var staffApp = reopened;
        Check("staff can register a product", staffApp.Products.Add("STF-1", "Staff Item", "Misc", 1m, 0, 0) != null);
        Throws("staff cannot delete products", () => staffApp.Products.Delete(staffApp.Products.FindBySku("STF-1")!.Id));
        Throws("staff cannot add suppliers", () => staffApp.Suppliers.Add("Nope", "", "", ""));
        Throws("staff cannot add users", () => staffApp.Auth.AddUser("sneaky", "secret1", Role.Admin));
        Check("staff can stock out", staffApp.Stock.StockOut(staffApp.Products.FindBySku("WID-1")!.Id, 1, "Staff sale") != null);

        staffApp.Auth.Logout();
        Throws("logged-out user cannot do anything", () => staffApp.Stock.StockOut(widget.Id, 1, "x"));
    }

    private static void Check(string name, bool condition)
    {
        if (condition) { _passed++; Console.WriteLine($"  PASS  {name}"); }
        else { _failed++; Console.WriteLine($"  FAIL  {name}"); }
    }

    private static void Throws(string name, Action action)
    {
        try
        {
            action();
            _failed++;
            Console.WriteLine($"  FAIL  {name} (no error was raised)");
        }
        catch (InventoryException)
        {
            _passed++;
            Console.WriteLine($"  PASS  {name}");
        }
    }
}
