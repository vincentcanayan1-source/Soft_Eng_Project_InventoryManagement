using System.Windows.Forms;

namespace InventoryManagement;

public class PurchasePage : PageBase
{
    private readonly DataGridView _grid = Ui.NewGrid();
    private readonly ComboBox _filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Button _markOrdered;
    private readonly Button _cancel;
    private Report? _report;

    public PurchasePage(InventoryApp app) : base(app)
    {
        _filter.Items.AddRange(new object[]
        {
            "All orders", "Open (Pending + Ordered)", "Pending", "Ordered", "Received", "Cancelled"
        });
        _filter.SelectedIndex = 1;
        _filter.SelectedIndexChanged += (_, _) => Ui.Run(RefreshData);

        _markOrdered = Ui.Btn("Mark as Ordered (Admin)", MarkOrdered);
        _cancel = Ui.Btn("Cancel order (Admin)", CancelOrder);

        Controls.Add(_grid);
        Controls.Add(Ui.Bar(
            Ui.Btn("New order", NewOrder), _markOrdered,
            Ui.Btn("Receive (adds stock)", Receive), _cancel,
            Ui.Btn("Export CSV", () => Ui.ExportReport(App, _report))));
        Controls.Add(Ui.Bar(Ui.Caption("Show:"), _filter));
    }

    public override void RefreshData()
    {
        var all = App.Purchases.List();
        IEnumerable<PurchaseOrder> list = _filter.SelectedIndex switch
        {
            1 => all.Where(o => o.Status == PurchaseStatus.Pending || o.Status == PurchaseStatus.Ordered),
            2 => all.Where(o => o.Status == PurchaseStatus.Pending),
            3 => all.Where(o => o.Status == PurchaseStatus.Ordered),
            4 => all.Where(o => o.Status == PurchaseStatus.Received),
            5 => all.Where(o => o.Status == PurchaseStatus.Cancelled),
            _ => all
        };

        _report = App.Reports.PurchaseOrders(list);
        Ui.FillGrid(_grid, _report);

        _markOrdered.Enabled = IsAdmin;
        _cancel.Enabled = IsAdmin;
    }

    private void NewOrder()
    {
        var choices = App.Products.List()
            .Where(p => p.SupplierId != null)
            .Select(p => $"{p.Sku} - {p.Name}")
            .ToArray();
        if (choices.Length == 0)
            throw new InventoryException("No product has a supplier yet. Assign suppliers on the Products tab first.");

        var r = Dialogs.Ask(this, "New purchase order",
            new Field("Product") { Kind = FieldKind.Choice, Choices = choices },
            new Field("Quantity") { Value = "10", Required = true });
        if (r == null) return;

        var product = App.Products.Resolve(r[0].Split(" - ")[0]);
        var order = App.Purchases.Create(product.Id, Ui.ParseInt(r[1], "Quantity", 1, StockService.MaxQuantityPerTransaction));
        RefreshData();
        Ui.Info($"Created PO-{order.Id} (Pending).");
    }

    private void MarkOrdered()
    {
        App.Purchases.MarkOrdered(Ui.SelectedId(_grid));
        RefreshData();
    }

    private void Receive()
    {
        var id = Ui.SelectedId(_grid);
        if (!Ui.Confirm($"Receive PO-{id}? This adds the ordered quantity to stock.")) return;
        App.Purchases.Receive(id);
        RefreshData();
        Ui.Info($"PO-{id} received and stock updated.");
    }

    private void CancelOrder()
    {
        var id = Ui.SelectedId(_grid);
        if (!Ui.Confirm($"Cancel PO-{id}?")) return;
        App.Purchases.Cancel(id);
        RefreshData();
    }
}
