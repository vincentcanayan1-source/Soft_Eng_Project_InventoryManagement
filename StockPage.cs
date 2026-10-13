using System.Drawing;
using System.Windows.Forms;

namespace InventoryManagement;

public class StockPage : PageBase
{
    /// <summary>One box with a product picker, quantity, note and a button (used for both Stock In and Stock Out).</summary>
    private sealed class MovePanel : GroupBox
    {
        public ComboBox ProductBox { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Left = 120, Top = 28, Width = 330 };
        public Label Info { get; } = new() { Left = 120, Top = 58, Width = 330, Height = 32, ForeColor = Color.DimGray };
        public NumericUpDown Quantity { get; } = new() { Left = 120, Top = 94, Width = 110, Minimum = 1, Maximum = StockService.MaxQuantityPerTransaction, Value = 1 };
        public TextBox Note { get; } = new() { Left = 120, Top = 128, Width = 330 };
        public Button Submit { get; }

        public MovePanel(string title, string noteLabel, string buttonText, Action<MovePanel> onClick)
        {
            Text = title;
            Width = 480;
            Height = 210;
            Margin = new Padding(8);

            Controls.Add(new Label { Text = "Product", Left = 15, Top = 32, Width = 100 });
            Controls.Add(new Label { Text = "Quantity", Left = 15, Top = 98, Width = 100 });
            Controls.Add(new Label { Text = noteLabel, Left = 15, Top = 132, Width = 100 });
            Controls.Add(ProductBox);
            Controls.Add(Info);
            Controls.Add(Quantity);
            Controls.Add(Note);

            Submit = new Button { Text = buttonText, Left = 120, Top = 164, Width = 170 };
            Submit.Click += (_, _) => Ui.Run(() => onClick(this));
            Controls.Add(Submit);
        }
    }

    private readonly MovePanel _in;
    private readonly MovePanel _out;
    private readonly DataGridView _recent = Ui.NewGrid();

    public StockPage(InventoryApp app) : base(app)
    {
        _in = new MovePanel("Stock In (receiving from supplier)", "Delivery / PO no.", "Receive stock", DoStockIn);
        _out = new MovePanel("Stock Out (issue / sale)", "Reason", "Issue stock", DoStockOut);
        _in.ProductBox.SelectedIndexChanged += (_, _) => ShowInfo(_in);
        _out.ProductBox.SelectedIndexChanged += (_, _) => ShowInfo(_out);

        var panels = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        panels.Controls.Add(_in);
        panels.Controls.Add(_out);

        var recentBox = new GroupBox { Text = "Latest transactions", Dock = DockStyle.Fill, Padding = new Padding(8) };
        recentBox.Controls.Add(_recent);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(panels, 0, 0);
        root.Controls.Add(recentBox, 0, 1);
        Controls.Add(root);
    }

    public override void RefreshData()
    {
        var products = App.Products.List();
        Ui.LoadProducts(_in.ProductBox, products, false);
        Ui.LoadProducts(_out.ProductBox, products, false);
        ShowInfo(_in);
        ShowInfo(_out);

        var latest = App.Stock.History().Take(20);
        Ui.FillGrid(_recent, App.Reports.TransactionLog(latest));
    }

    private void ShowInfo(MovePanel panel)
    {
        if (panel.ProductBox.SelectedItem is not ProductItem item)
        {
            panel.Info.Text = "";
            return;
        }
        var p = item.Product;
        panel.Info.Text = $"On hand: {p.Quantity}   Reorder level: {p.ReorderLevel}\nSupplier: {App.Suppliers.NameOf(p.SupplierId)}";
    }

    private void DoStockIn(MovePanel panel)
    {
        var product = Ui.RequireProduct(panel.ProductBox);
        var t = App.Stock.StockIn(product.Id, (int)panel.Quantity.Value, panel.Note.Text);
        panel.Note.Clear();
        panel.Quantity.Value = 1;
        RefreshData();
        Ui.Info($"Received {t.Quantity} x {product.Name}. New balance: {t.BalanceAfter}.");
    }

    private void DoStockOut(MovePanel panel)
    {
        var product = Ui.RequireProduct(panel.ProductBox);
        var t = App.Stock.StockOut(product.Id, (int)panel.Quantity.Value, panel.Note.Text);
        panel.Note.Clear();
        panel.Quantity.Value = 1;
        RefreshData();

        var message = $"Issued {t.Quantity} x {product.Name}. New balance: {t.BalanceAfter}.";
        if (ReorderService.IsLow(product))
            message += $"\n\nWARNING: stock is at or below the reorder level ({product.ReorderLevel}).";
        Ui.Info(message);
    }
}
