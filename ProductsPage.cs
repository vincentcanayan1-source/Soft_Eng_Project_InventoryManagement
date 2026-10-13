using System.Globalization;
using System.Windows.Forms;

namespace InventoryManagement;

public class ProductsPage : PageBase
{
    private readonly DataGridView _grid = Ui.NewGrid();
    private readonly TextBox _search = new() { Width = 200 };
    private readonly ComboBox _category = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _edit;
    private readonly Button _delete;
    private readonly Button _assign;

    public ProductsPage(InventoryApp app) : base(app)
    {
        Ui.StatusColors(_grid, "Status");
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Ui.Run(RefreshData);
            }
        };

        _edit = Ui.Btn("Edit (Admin)", EditProduct);
        _delete = Ui.Btn("Delete (Admin)", DeleteProduct);
        _assign = Ui.Btn("Assign supplier (Admin)", AssignSupplier);

        var searchBar = Ui.Bar(
            Ui.Caption("Search:"), _search,
            Ui.Caption("Category:"), _category,
            Ui.Btn("Search", RefreshData),
            Ui.Btn("Clear", () =>
            {
                _search.Clear();
                _category.SelectedIndex = 0;
                RefreshData();
            }));
        var actionBar = Ui.Bar(Ui.Btn("Register new product", RegisterProduct), _edit, _delete, _assign);

        Controls.Add(_grid);
        Controls.Add(actionBar);
        Controls.Add(searchBar);
    }

    public override void RefreshData()
    {
        var previous = _category.SelectedItem as string;
        _category.Items.Clear();
        _category.Items.Add("(all)");
        foreach (var c in App.Products.Categories()) _category.Items.Add(c);
        var index = previous == null ? 0 : _category.Items.IndexOf(previous);
        _category.SelectedIndex = index >= 0 ? index : 0;

        var category = _category.SelectedIndex > 0 ? _category.SelectedItem as string : null;
        var products = App.Products.Search(_search.Text, category);
        Ui.FillGrid(_grid, Ui.ProductHeaders, products.Select(p => Ui.ProductRow(App, p)));

        _edit.Enabled = IsAdmin;
        _delete.Enabled = IsAdmin;
        _assign.Enabled = IsAdmin;
    }

    private void RegisterProduct()
    {
        var r = Dialogs.Ask(this, "Register new product",
            new Field("SKU (no spaces)") { Required = true },
            new Field("Product name") { Required = true },
            new Field("Category") { Value = "General" },
            new Field("Unit price") { Value = "0" },
            new Field("Reorder level") { Value = "10" },
            new Field("Standard reorder quantity") { Value = "20" },
            new Field("Supplier") { Kind = FieldKind.Choice, Choices = Ui.SupplierChoices(App) });
        if (r == null) return;

        var product = App.Products.Add(r[0], r[1], r[2],
            Ui.ParseDecimal(r[3], "Unit price"),
            Ui.ParseInt(r[4], "Reorder level"),
            Ui.ParseInt(r[5], "Reorder quantity"),
            Ui.SupplierIdFrom(r[6]));
        RefreshData();
        Ui.Info($"Registered {product.Sku}. Use the Stock tab to receive quantity.");
    }

    private void EditProduct()
    {
        var p = App.Products.Get(Ui.SelectedId(_grid));
        var r = Dialogs.Ask(this, $"Edit {p.Sku}",
            new Field("Product name") { Value = p.Name, Required = true },
            new Field("Category") { Value = p.Category },
            new Field("Unit price") { Value = p.UnitPrice.ToString(CultureInfo.InvariantCulture) },
            new Field("Reorder level") { Value = p.ReorderLevel.ToString() },
            new Field("Standard reorder quantity") { Value = p.ReorderQuantity.ToString() });
        if (r == null) return;

        App.Products.Update(p.Id, r[0], r[1],
            Ui.ParseDecimal(r[2], "Unit price"),
            Ui.ParseInt(r[3], "Reorder level"),
            Ui.ParseInt(r[4], "Reorder quantity"));
        RefreshData();
    }

    private void DeleteProduct()
    {
        var p = App.Products.Get(Ui.SelectedId(_grid));
        if (!Ui.Confirm($"Delete {p.Sku} - {p.Name}?")) return;
        App.Products.Delete(p.Id);
        RefreshData();
    }

    private void AssignSupplier()
    {
        var p = App.Products.Get(Ui.SelectedId(_grid));
        var r = Dialogs.Ask(this, $"Supplier for {p.Sku}",
            new Field("Supplier") { Kind = FieldKind.Choice, Choices = Ui.SupplierChoices(App) });
        if (r == null) return;

        App.Products.AssignSupplier(p.Id, Ui.SupplierIdFrom(r[0]));
        RefreshData();
    }
}
