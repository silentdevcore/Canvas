namespace PXA.Application.UseCases;

public class AuthenticateUserUseCase
{
    private const string TokenSigningSecret = "canvas-secret-key";

    private readonly Dictionary<string, User> users = new()
    {
        ["admin"] = new User
        {
            Id = "user-admin",
            Username = "admin",
            Email = "admin@canvas.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        },
        ["demo"] = new User
        {
            Id = "user-demo",
            Username = "demo",
            Email = "demo@canvas.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo123"),
            Role = "user",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        }
    };

    public Task<AuthenticationResult> ExecuteAsync(AuthenticateUserRequest request)
    {
        var user = users.Values.FirstOrDefault(u =>
            (u.Username == request.Username || u.Email == request.Username) && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Task.FromResult(new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = "Invalid username or password"
            });
        }

        return Task.FromResult(new AuthenticationResult
        {
            IsSuccess = true,
            User = user,
            Token = GenerateJwtToken(user)
        });
    }

    public Task<User?> GetUserByIdAsync(string userId)
    {
        users.TryGetValue(userId, out var user);
        return Task.FromResult(user?.IsActive == true ? user : null);
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token) || !token.StartsWith("jwt.", StringComparison.Ordinal))
            return Task.FromResult(false);

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return Task.FromResult(false);

            var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            var expectedSignature = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(payload + TokenSigningSecret));

            return Task.FromResult(parts[2] == expectedSignature);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<string?> GetUserIdFromTokenAsync(string token)
    {
        if (!await ValidateTokenAsync(token))
            return null;

        var parts = token.Split('.');
        var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
        return payload.Split(':')[0];
    }

    private static string GenerateJwtToken(User user)
    {
        var payload = $"{user.Id}:{user.Username}:{user.Role}:{DateTime.UtcNow.Ticks}";
        var signature = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload + TokenSigningSecret));
        return $"jwt.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload))}.{signature}";
    }
}

public class AuthenticateUserRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public User? User { get; set; }
    public string? Token { get; set; }
}

public class User
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
