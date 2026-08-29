using System.Text.Json;

namespace Moto.Core.Plugins.Marketplace;

/// <summary>
/// Gestion des comptes utilisateurs marketplace.
/// </summary>
public sealed class MarketplaceAccountService
{
    private readonly string _accountFile;
    private UserAccount? _currentAccount;

    public MarketplaceAccountService()
    {
        _accountFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MotoEditor", "marketplace-account.json");
        LoadAccount();
    }

    public UserAccount? CurrentAccount => _currentAccount;
    public bool IsLoggedIn => _currentAccount != null;

    public async Task<UserAccount> LoginAsync(string email, string password)
    {
        // TODO: appel API AuthController
        var account = new UserAccount
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
        _currentAccount = account;
        await SaveAccountAsync();
        return account;
    }

    public async Task LogoutAsync()
    {
        _currentAccount = null;
        if (File.Exists(_accountFile)) File.Delete(_accountFile);
        await Task.CompletedTask;
    }

    private void LoadAccount()
    {
        if (!File.Exists(_accountFile)) return;
        try
        {
            var json = File.ReadAllText(_accountFile);
            _currentAccount = JsonSerializer.Deserialize<UserAccount>(json);
        }
        catch { _currentAccount = null; }
    }

    private async Task SaveAccountAsync()
    {
        var json = JsonSerializer.Serialize(_currentAccount, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_accountFile, json);
    }
}

public class UserAccount
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<string> Subscriptions { get; set; } = new();
    public List<string> Purchases { get; set; } = new();
}
