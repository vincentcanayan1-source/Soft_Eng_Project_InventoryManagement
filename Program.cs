using System.Windows.Forms;

namespace InventoryManagement;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            // Data, backups and CSV exports live in Documents\InventoryManagementSystem
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "InventoryManagementSystem");
            var app = new InventoryApp(Path.Combine(folder, "inventory.json"));

            while (true)
            {
                app.Auth.Logout();

                using (var login = new LoginForm(app))
                {
                    if (login.ShowDialog() != DialogResult.OK) break;
                }

                using var main = new MainForm(app);
                Application.Run(main);
                if (!main.LoggedOut) break;
            }
            return 0;
        }
        catch (InventoryException ex)
        {
            MessageBox.Show(ex.Message, Ui.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
