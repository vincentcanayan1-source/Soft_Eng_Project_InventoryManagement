using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace InventoryManagement;

public class MainForm : Form
{
    private readonly InventoryApp _app;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly List<PageBase> _pages = new();

    /// <summary>True when the user chose Log out (so the login screen should appear again).</summary>
    public bool LoggedOut { get; private set; }

    public MainForm(InventoryApp app)
    {
        _app = app;

        Text = Ui.AppTitle;
        Width = 1180;
        Height = 740;
        MinimumSize = new Size(940, 580);
        StartPosition = FormStartPosition.CenterScreen;

        AddPage("Dashboard", new DashboardPage(app));
        AddPage("Products", new ProductsPage(app));
        AddPage("Suppliers", new SuppliersPage(app));
        AddPage("Stock In / Out", new StockPage(app));
        AddPage("Transaction History", new HistoryPage(app));
        AddPage("Reorder", new ReorderPage(app));
        AddPage("Purchase Orders", new PurchasePage(app));
        AddPage("Reports", new ReportsPage(app));

        var user = app.Auth.CurrentUser;
        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel(
            $"Signed in as {user?.Username} ({user?.Role})    |    Data file: {app.Db.FilePath}"));

        Controls.Add(_tabs);
        Controls.Add(status);
        var menu = BuildMenu();
        MainMenuStrip = menu;
        Controls.Add(menu);

        _tabs.SelectedIndexChanged += (_, _) => RefreshCurrent();
        Shown += (_, _) => RefreshCurrent();
    }

    private void AddPage(string title, PageBase page)
    {
        var tab = new TabPage(title);
        tab.Controls.Add(page);
        _tabs.TabPages.Add(tab);
        _pages.Add(page);
    }

    private void RefreshCurrent()
    {
        if (_tabs.SelectedIndex < 0 || _tabs.SelectedIndex >= _pages.Count) return;
        Ui.Run(() => _pages[_tabs.SelectedIndex].RefreshData());
    }

    // ------------------------------------------------------------------ menu

    private static ToolStripMenuItem Item(string text, Action action) =>
        new ToolStripMenuItem(text, null, (_, _) => Ui.Run(action));

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();

        var account = new ToolStripMenuItem("Account");
        account.DropDownItems.Add(Item("Change my password...", ChangePassword));
        account.DropDownItems.Add(Item("Log out", () =>
        {
            LoggedOut = true;
            Close();
        }));
        account.DropDownItems.Add(Item("Exit", Close));
        menu.Items.Add(account);

        var admin = new ToolStripMenuItem("Administration") { Visible = _app.Auth.IsAdmin };
        admin.DropDownItems.Add(Item("Add user...", AddUser));
        admin.DropDownItems.Add(Item("View users", ViewUsers));
        admin.DropDownItems.Add(new ToolStripSeparator());
        admin.DropDownItems.Add(Item("Create database backup", CreateBackup));
        admin.DropDownItems.Add(Item("Restore from backup...", RestoreBackup));
        admin.DropDownItems.Add(Item("Open data folder", OpenDataFolder));
        menu.Items.Add(admin);

        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add(Item("Load sample data (empty database only)", LoadSampleData));
        help.DropDownItems.Add(Item("Run self-test", RunSelfTest));
        help.DropDownItems.Add(Item("About", () => Ui.Info(
            $"{Ui.AppTitle}\nVincent Canayan, BSIT-2A\n\nData folder:\n{Path.GetDirectoryName(_app.Db.FilePath)}")));
        menu.Items.Add(help);

        return menu;
    }

    private void ChangePassword()
    {
        var r = Dialogs.Ask(this, "Change password",
            new Field("Current password") { Kind = FieldKind.Password, Required = true },
            new Field("New password (min 6)") { Kind = FieldKind.Password, Required = true },
            new Field("Repeat new password") { Kind = FieldKind.Password, Required = true });
        if (r == null) return;
        if (r[1] != r[2]) throw new InventoryException("The new passwords do not match.");

        _app.Auth.ChangePassword(r[0], r[1]);
        Ui.Info("Password changed.");
    }

    private void AddUser()
    {
        var r = Dialogs.Ask(this, "Add user",
            new Field("Username") { Required = true },
            new Field("Password (min 6)") { Kind = FieldKind.Password, Required = true },
            new Field("Role") { Kind = FieldKind.Choice, Choices = new[] { "Staff", "Admin" } });
        if (r == null) return;

        _app.Auth.AddUser(r[0], r[1], r[2] == "Admin" ? Role.Admin : Role.Staff);
        Ui.Info("User created.");
    }

    private void ViewUsers()
    {
        var users = _app.Auth.ListUsers();
        var text = TextTable.Render(new[] { "Username", "Role" },
            users.Select(u => new[] { u.Username, u.Role.ToString() }));
        Ui.ShowText(this, "Users", text);
    }

    private void CreateBackup()
    {
        _app.Auth.RequireAdmin();
        var path = _app.Db.Backup();
        Ui.Info("Backup saved:\n\n" + path);
    }

    private void RestoreBackup()
    {
        _app.Auth.RequireAdmin();
        var backups = _app.Db.ListBackups();
        if (backups.Count == 0) throw new InventoryException("There are no backups yet. Create one first.");

        var names = backups.Select(b => Path.GetFileName(b) ?? "").ToArray();
        var r = Dialogs.Ask(this, "Restore from backup",
            new Field("Backup file") { Kind = FieldKind.Choice, Choices = names });
        if (r == null) return;

        if (!Ui.Confirm("This replaces ALL current data (a safety backup is made first). Continue?")) return;

        var chosen = backups.First(b => Path.GetFileName(b) == r[0]);
        _app.Db.Restore(chosen);

        if (!_app.Auth.Refresh())
        {
            Ui.Warn("Restore complete, but your account is not in that backup. Please log in again.");
            LoggedOut = true;
            Close();
            return;
        }
        RefreshCurrent();
        Ui.Info("Database restored.");
    }

    private void OpenDataFolder()
    {
        var folder = Path.GetDirectoryName(_app.Db.FilePath)!;
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private void LoadSampleData()
    {
        _app.Auth.RequireAdmin();
        if (_app.Db.Data.Products.Count > 0 || _app.Db.Data.Suppliers.Count > 0)
            throw new InventoryException("Sample data can only be loaded into an empty database.");

        SampleData.Load(_app);
        RefreshCurrent();
        Ui.Info("Sample suppliers, products and transactions were added.");
    }

    private void RunSelfTest()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            SelfTest.Run();
        }
        finally
        {
            Console.SetOut(original);
        }
        Ui.ShowText(this, "Self-test results", output.ToString());
    }
}
