namespace Moto.Marketplace.Api.Services;

public class AccountService
{
    private readonly Dictionary<string, UserAccountEntity> _accounts = new();

    public Task<UserAccountEntity> CreateAsync(string email, string password)
    {
        var account = new UserAccountEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };
        _accounts[account.Id] = account;
        return Task.FromResult(account);
    }

    public Task<string?> AuthenticateAsync(string email, string password)
    {
        var account = _accounts.Values.FirstOrDefault(a => a.Email == email);
        if (account == null || account.PasswordHash != HashPassword(password))
            return Task.FromResult<string?>(null);

        // TODO: générer JWT
        return Task.FromResult<string?>("fake-jwt-token");
    }

    public UserAccountEntity? GetById(string id) =>
        _accounts.TryGetValue(id, out var acc) ? acc : null;

    private static string HashPassword(string password) =>
        Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(password)));
}

public class UserAccountEntity
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
