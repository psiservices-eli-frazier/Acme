using System.Security.Cryptography;
using Acme.Server.Data;
using Acme.Server.Data.Repositories;
using Acme.Server.Domain;

namespace Acme.Server.Security;

/// <summary>
/// <c>dotnet run -- --create-admin &lt;username&gt;</c>.
///
/// Acme's own notes called out that provisioning a first account outside development
/// was unsolved: the only accounts anywhere came from the dev seeder, and there is no
/// screen or endpoint for user management. This is the missing front door, and nothing
/// more than that -- it is not the beginning of a user-management feature.
///
/// The password comes from <c>ACME_ADMIN_PASSWORD</c> if set, so it need not appear in
/// shell history; otherwise one is generated and printed once.
/// </summary>
internal static class AdminProvisioning
{
    public static async Task RunAsync(IServiceProvider services, string[] args)
    {
        var index = Array.IndexOf(args, "--create-admin");
        var username = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;

        if (string.IsNullOrWhiteSpace(username) || username.StartsWith('-'))
        {
            await Console.Error.WriteLineAsync("Usage: --create-admin <username>");
            Environment.ExitCode = 1;
            return;
        }

        using var scope = services.CreateScope();
        var connections = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var users = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var auth = scope.ServiceProvider.GetRequiredService<SignInService>();

        await using var connection = await connections.OpenWithTransactionAsync();

        if (await users.ExistsByUsernameAsync(connection, username))
        {
            await Console.Error.WriteLineAsync($"An account named '{username}' already exists.");
            Environment.ExitCode = 1;
            return;
        }

        var supplied = Environment.GetEnvironmentVariable("ACME_ADMIN_PASSWORD");
        var password = string.IsNullOrWhiteSpace(supplied) ? GeneratePassword() : supplied;

        await users.InsertAsync(connection, new AppUser
        {
            Username = username,
            PasswordHash = auth.Hash(password),
            DisplayName = username,
            Enabled = true,
            Roles = [Role.Admin],
        });

        connection.Commit();

        Console.WriteLine($"Created administrator '{username.ToLowerInvariant()}'.");
        if (string.IsNullOrWhiteSpace(supplied))
        {
            Console.WriteLine($"Generated password (shown once): {password}");
        }
    }

    private static string GeneratePassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).TrimEnd('=');
}
