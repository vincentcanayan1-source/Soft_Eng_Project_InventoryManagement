using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace InventoryManagement;

/// <summary>Wraps a product so it can be shown in a ComboBox.</summary>
public class ProductItem
{
    public Product Product { get; }
    public ProductItem(Product product) => Product = product;
    public override string ToString() => $"{Product.Sku} - {Product.Name}  (on hand: {Product.Quantity})";
}

/// <summary>Helpers shared by all screens.</summary>
public static class Ui
{
    public const string AppTitle = "Inventory Management System";

    public static readonly string[] ProductHeaders =
        { "ID", "SKU", "Product", "Category", "Price", "Qty", "Reorder", "Status", "Supplier" };

    public static string[] ProductRow(InventoryApp app, Product p) => new[]
    {
        p.Id.ToString(), p.Sku, p.Name, p.Category, ReportService.Money(p.UnitPrice),
        p.Quantity.ToString(), p.ReorderLevel.ToString(), ReorderService.StatusOf(p),
        app.Suppliers.NameOf(p.SupplierId)
    };

    // ---------------------------------------------------------- messages

    /// <summary>Runs a button action and shows a friendly message if the business rules refuse it.</summary>
    public static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (InventoryException ex)
        {
            Warn(ex.Message);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Unexpected problem: " + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static void Info(string message) =>
        MessageBox.Show(message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(string message) =>
        MessageBox.Show(message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool Confirm(string question) =>
        MessageBox.Show(question, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static void ShowText(IWin32Window owner, string title, string text)
    {
        using var form = new Form
        {
            Text = title,
            Width = 760,
            Height = 560,
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false
        };
        var box = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10f),
            Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n")
        };
        form.Controls.Add(box);
        box.Select(0, 0);
        form.ShowDialog(owner);
    }

    // ----------------------------------------------------------- parsing

    public static int ParseInt(string? text, string field, int min = 0, int max = int.MaxValue)
    {
        if (!int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value < min || value > max)
            throw new InventoryException($"{field} must be a whole number from {min:N0} to {max:N0}.");
        return value;
    }

    public static decimal ParseDecimal(string? text, string field, decimal min = 0, decimal max = 10_000_000m)
    {
        if (!decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            || value < min || value > max)
            throw new InventoryException($"{field} must be a number from {min:N0} to {max:N0}.");
        return value;
    }

    // ------------------------------------------------------------ grids

    public static DataGridView NewGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells
        };
    }

    public static void FillGrid(DataGridView grid, string[] headers, IEnumerable<string[]> rows)
    {
        var table = new DataTable();
        foreach (var header in headers)
            table.Columns.Add(header, typeof(string));
        foreach (var row in rows)
            table.Rows.Add(row.Cast<object>().ToArray());
        grid.DataSource = table;
    }

    public static void FillGrid(DataGridView grid, Report report) =>
        FillGrid(grid, report.Headers, report.Rows);

    /// <summary>The number in the first column of the selected row, or null if nothing is selected.</summary>
    public static int? TryId(DataGridView grid)
    {
        var cell = grid.CurrentRow?.Cells[0].Value;
        return int.TryParse(Convert.ToString(cell), out var id) ? id : null;
    }

    public static int SelectedId(DataGridView grid) =>
        TryId(grid) ?? throw new InventoryException("Select a row in the table first.");

    /// <summary>Tints rows whose status column says LOW (yellow) or OUT (red).</summary>
    public static void StatusColors(DataGridView grid, string column)
    {
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count || !grid.Columns.Contains(column)) return;
            var status = Convert.ToString(grid.Rows[e.RowIndex].Cells[column].Value);
            if (status == "OUT") e.CellStyle!.BackColor = Color.MistyRose;
            else if (status == "LOW") e.CellStyle!.BackColor = Color.LightYellow;
        };
    }

    // ---------------------------------------------------------- controls

    public static Button Btn(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(8, 3, 8, 3),
            Margin = new Padding(4)
        };
        button.Click += (_, _) => Run(onClick);
        return button;
    }

    public static Label Caption(string text) =>
        new Label { Text = text, AutoSize = true, Margin = new Padding(8, 8, 2, 0) };

    public static FlowLayoutPanel Bar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6, 6, 6, 2)
        };
        bar.Controls.AddRange(controls);
        return bar;
    }

    /// <summary>Fills a combo box with products and keeps the previous selection when possible.</summary>
    public static void LoadProducts(ComboBox combo, IEnumerable<Product> products, bool includeAll)
    {
        var keep = (combo.SelectedItem as ProductItem)?.Product.Id;
        combo.BeginUpdate();
        combo.Items.Clear();
        if (includeAll) combo.Items.Add("(all products)");
        foreach (var p in products) combo.Items.Add(new ProductItem(p));
        combo.EndUpdate();

        var match = combo.Items.OfType<ProductItem>().FirstOrDefault(i => i.Product.Id == keep);
        if (match != null) combo.SelectedItem = match;
        else if (includeAll) combo.SelectedIndex = 0;
        else combo.SelectedIndex = -1;
    }

    public static Product RequireProduct(ComboBox combo) =>
        (combo.SelectedItem as ProductItem)?.Product ?? throw new InventoryException("Choose a product first.");

    public static string[] SupplierChoices(InventoryApp app) =>
        new[] { "(none)" }.Concat(app.Suppliers.List().Select(s => $"{s.Id} - {s.Name}")).ToArray();

    public static int? SupplierIdFrom(string choice)
    {
        var first = (choice ?? "").Split(' ')[0];
        return int.TryParse(first, out var id) ? id : null;
    }

    /// <summary>Saves the report as CSV in the reports folder and tells the user where it went.</summary>
    public static void ExportReport(InventoryApp app, Report? report)
    {
        if (report == null || report.Rows.Count == 0)
            throw new InventoryException("There is nothing to export yet. Show a report first.");
        var path = app.Reports.ExportCsv(report, app.ReportsDirectory);
        Info("Saved CSV file (opens in Excel):\n\n" + path);
    }
}

public enum FieldKind { Text, Password, Choice }

public class Field
{
    public string Label { get; }
    public string Value { get; set; } = "";
    public FieldKind Kind { get; set; } = FieldKind.Text;
    public string[] Choices { get; set; } = Array.Empty<string>();
    public bool Required { get; set; }

    public Field(string label) => Label = label;
}

/// <summary>A small pop-up form that collects a few values.</summary>
public static class Dialogs
{
    /// <summary>Returns the entered values in field order, or null if the user cancelled.</summary>
    public static string[]? Ask(IWin32Window owner, string title, params Field[] fields)
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        var inputs = new List<Control>();
        var y = 16;
        foreach (var field in fields)
        {
            form.Controls.Add(new Label
            {
                Text = field.Label + (field.Required ? " *" : ""),
                Left = 16,
                Top = y + 4,
                Width = 170
            });

            Control input;
            if (field.Kind == FieldKind.Choice)
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 190, Top = y, Width = 260 };
                combo.Items.AddRange(field.Choices);
                var index = Array.IndexOf(field.Choices, field.Value);
                combo.SelectedIndex = index >= 0 ? index : (field.Choices.Length > 0 ? 0 : -1);
                input = combo;
            }
            else
            {
                input = new TextBox
                {
                    Left = 190,
                    Top = y,
                    Width = 260,
                    Text = field.Value,
                    UseSystemPasswordChar = field.Kind == FieldKind.Password
                };
            }
            form.Controls.Add(input);
            inputs.Add(input);
            y += 34;
        }

        var ok = new Button { Text = "OK", Left = 270, Top = y + 8, Width = 85 };
        var cancel = new Button { Text = "Cancel", Left = 365, Top = y + 8, Width = 85, DialogResult = DialogResult.Cancel };
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.ClientSize = new Size(470, y + 52);

        ok.Click += (_, _) =>
        {
            for (var i = 0; i < fields.Length; i++)
            {
                if (fields[i].Required && inputs[i].Text.Trim().Length == 0)
                {
                    MessageBox.Show($"{fields[i].Label} is required.", Ui.AppTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    inputs[i].Focus();
                    return;
                }
            }
            form.DialogResult = DialogResult.OK;
        };
        form.Shown += (_, _) => { if (inputs.Count > 0) inputs[0].Focus(); };

        if (form.ShowDialog(owner) != DialogResult.OK) return null;

        var result = new string[fields.Length];
        for (var i = 0; i < fields.Length; i++)
            result[i] = fields[i].Kind == FieldKind.Password ? inputs[i].Text : inputs[i].Text.Trim();
        return result;
    }
}

/// <summary>Base class for each tab. RefreshData reloads the screen from the data.</summary>
public abstract class PageBase : UserControl
{
    protected InventoryApp App { get; }
    protected bool IsAdmin => App.Auth.IsAdmin;

    protected PageBase(InventoryApp app)
    {
        App = app;
        Dock = DockStyle.Fill;
    }

    public abstract void RefreshData();
}
