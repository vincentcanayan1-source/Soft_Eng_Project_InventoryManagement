using System.Windows.Forms;

namespace InventoryManagement;

public class SuppliersPage : PageBase
{
    private static readonly string[] Headers = { "ID", "Name", "Contact person", "Phone", "Email" };

    private readonly DataGridView _grid = Ui.NewGrid();
    private readonly DataGridView _products = Ui.NewGrid();
    private readonly Button _add;
    private readonly Button _edit;
    private readonly Button _delete;

    public SuppliersPage(InventoryApp app) : base(app)
    {
        Ui.StatusColors(_products, "Status");
        _grid.SelectionChanged += (_, _) => ShowProducts();

        _add = Ui.Btn("Add supplier (Admin)", AddSupplier);
        _edit = Ui.Btn("Edit (Admin)", EditSupplier);
        _delete = Ui.Btn("Delete (Admin)", DeleteSupplier);

        var lower = new GroupBox { Text = "Products from the selected supplier", Dock = DockStyle.Fill, Padding = new Padding(8) };
        lower.Controls.Add(_products);

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        split.Controls.Add(_grid, 0, 0);
        split.Controls.Add(lower, 0, 1);

        Controls.Add(split);
        Controls.Add(Ui.Bar(_add, _edit, _delete));
    }

    public override void RefreshData()
    {
        Ui.FillGrid(_grid, Headers,
            App.Suppliers.List().Select(s => new[] { s.Id.ToString(), s.Name, s.ContactPerson, s.Phone, s.Email }));
        ShowProducts();

        _add.Enabled = IsAdmin;
        _edit.Enabled = IsAdmin;
        _delete.Enabled = IsAdmin;
    }

    private void ShowProducts()
    {
        var id = Ui.TryId(_grid);
        var products = id.HasValue ? App.Suppliers.ProductsOf(id.Value) : new List<Product>();
        Ui.FillGrid(_products, Ui.ProductHeaders, products.Select(p => Ui.ProductRow(App, p)));
    }

    private void AddSupplier()
    {
        var r = Dialogs.Ask(this, "Add supplier",
            new Field("Name") { Required = true },
            new Field("Contact person"),
            new Field("Phone"),
            new Field("Email"));
        if (r == null) return;

        App.Suppliers.Add(r[0], r[1], r[2], r[3]);
        RefreshData();
    }

    private void EditSupplier()
    {
        var s = App.Suppliers.Get(Ui.SelectedId(_grid));
        var r = Dialogs.Ask(this, $"Edit {s.Name}",
            new Field("Name") { Value = s.Name, Required = true },
            new Field("Contact person") { Value = s.ContactPerson },
            new Field("Phone") { Value = s.Phone },
            new Field("Email") { Value = s.Email });
        if (r == null) return;

        App.Suppliers.Update(s.Id, r[0], r[1], r[2], r[3]);
        RefreshData();
    }

    private void DeleteSupplier()
    {
        var s = App.Suppliers.Get(Ui.SelectedId(_grid));
        if (!Ui.Confirm($"Delete supplier '{s.Name}'?")) return;
        App.Suppliers.Delete(s.Id);
        RefreshData();
    }
}
