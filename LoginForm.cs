using System.Drawing;
using System.Windows.Forms;

namespace InventoryManagement;

public class LoginForm : Form
{
    private readonly InventoryApp _app;
    private readonly TextBox _username = new() { Left = 120, Top = 70, Width = 220 };
    private readonly TextBox _password = new() { Left = 120, Top = 104, Width = 220, UseSystemPasswordChar = true };
    private int _attempts;

    public LoginForm(InventoryApp app)
    {
        _app = app;

        Text = Ui.AppTitle + " - Login";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(380, 230);

        Controls.Add(new Label
        {
            Text = "Inventory Management System",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            Left = 20,
            Top = 18,
            Width = 340,
            Height = 30
        });
        Controls.Add(new Label { Text = "Username", Left = 20, Top = 74, Width = 90 });
        Controls.Add(new Label { Text = "Password", Left = 20, Top = 108, Width = 90 });
        Controls.Add(_username);
        Controls.Add(_password);

        var login = new Button { Text = "Log in", Left = 120, Top = 146, Width = 100 };
        var exit = new Button { Text = "Exit", Left = 240, Top = 146, Width = 100, DialogResult = DialogResult.Cancel };
        login.Click += (_, _) => TryLogin();
        Controls.Add(login);
        Controls.Add(exit);
        AcceptButton = login;
        CancelButton = exit;

        Controls.Add(new Label
        {
            Text = "First time? Use  admin / admin123  and change the password afterwards.",
            ForeColor = Color.DimGray,
            Left = 20,
            Top = 190,
            Width = 340,
            Height = 32
        });
    }

    private void TryLogin()
    {
        if (_app.Auth.Login(_username.Text, _password.Text))
        {
            DialogResult = DialogResult.OK;
            return;
        }

        _attempts++;
        _password.Clear();
        if (_attempts >= 3)
        {
            MessageBox.Show("Too many failed attempts.", Ui.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            return;
        }
        MessageBox.Show($"Invalid username or password ({_attempts}/3).", Ui.AppTitle,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        _password.Focus();
    }
}
