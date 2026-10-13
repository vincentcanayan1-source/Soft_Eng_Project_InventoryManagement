using System.Drawing;
using System.Windows.Forms;

namespace InventoryManagement;

public class DashboardPage : PageBase
{
    private static readonly string[] CardNames =
        { "Products", "Suppliers", "Units in stock", "Stock value", "Low stock", "Out of stock", "Open purchase orders" };

    private readonly Label[] _values = new Label[CardNames.Length];
    private readonly DataGridView _top = Ui.NewGrid();
    private readonly DataGridView _recent = Ui.NewGrid();

    public DashboardPage(InventoryApp app) : base(app)
    {
        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8)
        };
        for (var i = 0; i < CardNames.Length; i++)
        {
            var panel = new Panel
            {
                Width = 150,
                Height = 74,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(6)
            };
            var caption = new Label
            {
                Text = CardNames[i],
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = Color.DimGray
            };
            _values[i] = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold)
            };
            panel.Controls.Add(caption);
            panel.Controls.Add(_values[i]);
            cards.Controls.Add(panel);
        }

        var topBox = new GroupBox { Text = "Top products by stock value", Dock = DockStyle.Fill, Padding = new Padding(8) };
        topBox.Controls.Add(_top);
        var recentBox = new GroupBox { Text = "Recent transactions", Dock = DockStyle.Fill, Padding = new Padding(8) };
        recentBox.Controls.Add(_recent);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.Controls.Add(topBox, 0, 0);
        bottom.Controls.Add(recentBox, 1, 0);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(cards, 0, 0);
        root.Controls.Add(bottom, 0, 1);
        Controls.Add(root);
    }

    public override void RefreshData()
    {
        var d = App.Reports.Dashboard();
        _values[0].Text = d.ProductCount.ToString();
        _values[1].Text = d.SupplierCount.ToString();
        _values[2].Text = d.TotalUnits.ToString("N0");
        _values[3].Text = ReportService.Money(d.TotalValue);
        _values[4].Text = d.LowStockCount.ToString();
        _values[5].Text = d.OutOfStockCount.ToString();
        _values[6].Text = d.OpenPurchaseOrders.ToString();
        _values[4].ForeColor = d.LowStockCount > 0 ? Color.DarkOrange : Color.Black;
        _values[5].ForeColor = d.OutOfStockCount > 0 ? Color.Firebrick : Color.Black;

        Ui.FillGrid(_top, new[] { "SKU", "Product", "Qty", "Value" },
            d.TopByValue.Select(p => new[] { p.Sku, p.Name, p.Quantity.ToString(), ReportService.Money(p.StockValue) }));
        Ui.FillGrid(_recent, App.Reports.TransactionLog(d.RecentTransactions));
    }
}
