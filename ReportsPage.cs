using System.Windows.Forms;

namespace InventoryManagement;

public class ReportsPage : PageBase
{
    private readonly DataGridView _grid = Ui.NewGrid();
    private readonly ComboBox _which = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly Label _title = new() { Dock = DockStyle.Top, Height = 28, Padding = new Padding(8, 6, 0, 0) };
    private Report? _report;

    public ReportsPage(InventoryApp app) : base(app)
    {
        _which.Items.AddRange(new object[]
        {
            "Current stock levels",
            "Low stock / reorder",
            "Inventory valuation by category",
            "Stock movement (uses the dates)",
            "Purchase orders"
        });
        _which.SelectedIndex = 0;
        _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _to.Value = DateTime.Today;

        Ui.StatusColors(_grid, "Status");

        Controls.Add(_grid);
        Controls.Add(_title);
        Controls.Add(Ui.Bar(
            Ui.Caption("Report:"), _which,
            Ui.Caption("From:"), _from,
            Ui.Caption("To:"), _to,
            Ui.Btn("Show", RefreshData),
            Ui.Btn("Export CSV", () => Ui.ExportReport(App, _report))));
    }

    public override void RefreshData()
    {
        _report = _which.SelectedIndex switch
        {
            1 => App.Reports.LowStock(),
            2 => App.Reports.Valuation(),
            3 => App.Reports.TransactionSummary(_from.Value.Date, _to.Value.Date),
            4 => App.Reports.PurchaseOrders(App.Purchases.List()),
            _ => App.Reports.StockLevels()
        };
        _title.Text = _report.Title;
        Ui.FillGrid(_grid, _report);
    }
}
