using System.Windows.Forms;

namespace InventoryManagement;

public class HistoryPage : PageBase
{
    private readonly DataGridView _grid = Ui.NewGrid();
    private readonly ComboBox _product = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 120 };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 120 };
    private Report? _report;

    public HistoryPage(InventoryApp app) : base(app)
    {
        _type.Items.AddRange(new object[] { "All types", "Stock in", "Stock out" });
        _type.SelectedIndex = 0;

        Controls.Add(_grid);
        Controls.Add(Ui.Bar(
            Ui.Caption("Product:"), _product,
            Ui.Caption("Type:"), _type,
            Ui.Caption("From:"), _from,
            Ui.Caption("To:"), _to,
            Ui.Btn("Search", Search),
            Ui.Btn("Export CSV", () => Ui.ExportReport(App, _report))));
    }

    public override void RefreshData()
    {
        Ui.LoadProducts(_product, App.Products.List(), true);
        Search();
    }

    private void Search()
    {
        int? productId = (_product.SelectedItem as ProductItem)?.Product.Id;
        TransactionType? type = _type.SelectedIndex switch
        {
            1 => TransactionType.StockIn,
            2 => TransactionType.StockOut,
            _ => null
        };
        DateTime? from = _from.Checked ? _from.Value.Date : null;
        DateTime? to = _to.Checked ? _to.Value.Date : null;

        var list = App.Stock.History(productId, type, from, to);
        _report = App.Reports.TransactionLog(list);
        Ui.FillGrid(_grid, _report);
    }
}
