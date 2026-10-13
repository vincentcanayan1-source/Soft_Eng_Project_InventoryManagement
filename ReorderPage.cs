using System.Windows.Forms;

namespace InventoryManagement;

public class ReorderPage : PageBase
{
    private static readonly string[] Headers =
        { "ID", "SKU", "Product", "On hand", "Reorder level", "Suggested qty", "Supplier", "Note" };

    private readonly DataGridView _grid = Ui.NewGrid();

    public ReorderPage(InventoryApp app) : base(app)
    {
        Controls.Add(_grid);
        Controls.Add(Ui.Bar(
            Ui.Btn("Create purchase order for selected", CreateForSelected),
            Ui.Btn("Create purchase orders for ALL", CreateForAll),
            Ui.Btn("Refresh", RefreshData)));
        Controls.Add(new Label
        {
            Text = "Products at or below their reorder level. Suggested quantity brings stock to twice the reorder level (never less than the standard reorder quantity).",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 36,
            Padding = new Padding(8, 8, 8, 0)
        });
    }

    public override void RefreshData()
    {
        var recs = App.Reorder.Recommend();
        Ui.FillGrid(_grid, Headers, recs.Select(r => new[]
        {
            r.Product.Id.ToString(), r.Product.Sku, r.Product.Name, r.Product.Quantity.ToString(),
            r.Product.ReorderLevel.ToString(), r.SuggestedQuantity.ToString(), r.SupplierName,
            r.Product.SupplierId == null ? "no supplier" : r.AlreadyOnOrder ? "already on order" : ""
        }));
    }

    private void CreateForSelected()
    {
        var product = App.Products.Get(Ui.SelectedId(_grid));
        var order = App.Purchases.Create(product.Id, ReorderService.SuggestQuantity(product));
        RefreshData();
        Ui.Info($"Created purchase order PO-{order.Id} for {order.Quantity} x {product.Name} (Pending).");
    }

    private void CreateForAll()
    {
        var pending = App.Reorder.Recommend().Count(r => !r.AlreadyOnOrder && r.Product.SupplierId != null);
        if (pending == 0)
        {
            Ui.Info("Nothing to order: every low-stock product is already on order or has no supplier.");
            return;
        }
        if (!Ui.Confirm($"Create {pending} Pending purchase order(s)?")) return;

        var created = App.Purchases.CreateFromRecommendations();
        RefreshData();
        Ui.Info($"Created {created.Count} purchase order(s). Find them on the Purchase Orders tab.");
    }
}
