using System.Security.Cryptography;

namespace InventoryManagement;

/// <summary>User authentication and roles (WBS task 16).</summary>
public class AuthService
{
    private const int Iterations = 100_000;
    private readonly Database _db;

    public AuthService(Database db) => _db = db;

    public User? CurrentUser { get; private set; }
    public bool IsAdmin => CurrentUser?.Role == Role.Admin;
    public string Actor => CurrentUser?.Username ?? "system";

    /// <summary>First run only: creates admin / admin123 so someone can sign in.</summary>
    public void EnsureDefaultAdmin()
    {
        if (_db.Data.Users.Count > 0) return;
        _db.Data.Users.Add(CreateUser("admin", "admin123", Role.Admin));
        _db.Save();
    }

    public bool Login(string username, string password)
    {
        var name = (username ?? "").Trim();
        var user = _db.Data.Users.FirstOrDefault(u => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (user == null || !Verify(user, password ?? "")) return false;
        CurrentUser = user;
        return true;
    }

    public void Logout() => CurrentUser = null;

    /// <summary>Re-attaches the signed-in user to the current data (needed after a restore). Returns false if the user no longer exists.</summary>
    public bool Refresh()
    {
        if (CurrentUser == null) return false;
        var name = CurrentUser.Username;
        CurrentUser = _db.Data.Users.FirstOrDefault(u => u.Username.Equals(name, StringComparison.OrdinalIgnoreCase));
        return CurrentUser != null;
    }

    /// <summary>Runs an action as another user, then restores the previous sign-in (used for demo data).</summary>
    public void RunAs(User user, Action action)
    {
        var previous = CurrentUser;
        CurrentUser = user;
        try { action(); }
        finally { CurrentUser = previous; }
    }

    public void RequireLogin()
    {
        if (CurrentUser == null) throw new InventoryException("You must be logged in.");
    }

    public void RequireAdmin()
    {
        RequireLogin();
        if (!IsAdmin) throw new InventoryException("Access denied: this action requires an Admin account.");
    }

    public User AddUser(string username, string password, Role role)
    {
        RequireAdmin();
        username = Guard.Text(username, "Username", 30);
        if (username.Length < 3 || username.Contains(' '))
            throw new InventoryException("Username must be at least 3 characters with no spaces.");
        ValidatePassword(password);
        if (_db.Data.Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            throw new InventoryException($"Username '{username}' is already taken.");

        var user = CreateUser(username, password, role);
        _db.Data.Users.Add(user);
        _db.Save();
        return user;
    }

    public void ChangePassword(string oldPassword, string newPassword)
    {
        RequireLogin();
        if (!Verify(CurrentUser!, oldPassword ?? ""))
            throw new InventoryException("Current password is incorrect.");
        ValidatePassword(newPassword);

        var updated = CreateUser(CurrentUser!.Username, newPassword, CurrentUser.Role);
        CurrentUser.Salt = updated.Salt;
        CurrentUser.PasswordHash = updated.PasswordHash;
        _db.Save();
    }

    public List<User> ListUsers()
    {
        RequireAdmin();
        return _db.Data.Users.OrderBy(u => u.Username).ToList();
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            throw new InventoryException("Password must be at least 6 characters.");
    }

    private static User CreateUser(string username, string password, Role role)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return new User
        {
            Username = username,
            Role = role,
            Salt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash)
        };
    }

    private static bool Verify(User user, string password)
    {
        var salt = Convert.FromBase64String(user.Salt);
        var expected = Convert.FromBase64String(user.PasswordHash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
