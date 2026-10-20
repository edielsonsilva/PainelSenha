using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class DatabaseSetupTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task ResetAdmin_WithoutPassword_GeneratesPasswordThatWorksAndUnlocksTheAccount()
    {
        // Arrange
        await LockAdminAsync();

        try
        {
            // Act
            var result = await DatabaseSetup.ResetAdminAsync(App.Services, Config());

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.WasGenerated.ShouldBeTrue();
            result.Value.UserName.ShouldBe("admin");
            result.Value.Password.Length.ShouldBe(16);
            (await CheckPasswordAsync("admin", result.Value.Password)).ShouldBeTrue();
            (await CheckPasswordAsync("admin", AppFixture.TestPassword)).ShouldBeFalse();
            await using var db = await App.CreateDbContextAsync();
            var admin = await db.Users.SingleAsync(x => x.UserName == "admin");
            (admin.LockoutEnd is null).ShouldBeTrue();
            admin.AccessFailedCount.ShouldBe(0);
        }
        finally
        {
            await RestoreAdminPasswordAsync();
        }
    }

    [Fact]
    public async Task ResetAdmin_WithConfiguredPassword_UsesItAndDoesNotMarkItAsGenerated()
    {
        try
        {
            // Act
            var result = await DatabaseSetup.ResetAdminAsync(App.Services, Config(("Setup:AdminPassword", "Outra#Senha2026k")));

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.WasGenerated.ShouldBeFalse();
            (await CheckPasswordAsync("admin", "Outra#Senha2026k")).ShouldBeTrue();
        }
        finally
        {
            await RestoreAdminPasswordAsync();
        }
    }

    [Fact]
    public async Task ResetAdmin_WeakConfiguredPassword_ReturnsValidationErrorAndKeepsOldPassword()
    {
        // Act
        var result = await DatabaseSetup.ResetAdminAsync(App.Services, Config(("Setup:AdminPassword", "fraca")));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
        (await CheckPasswordAsync("admin", AppFixture.TestPassword)).ShouldBeTrue();
    }

    [Fact]
    public async Task ResetAdmin_UnknownUser_ReturnsNotFound()
    {
        // Act
        var result = await DatabaseSetup.ResetAdminAsync(App.Services, Config(("Setup:AdminUser", "inexistente")));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task ResetAdmin_NonAdminUser_IsNotAccepted()
    {
        // Act
        var result = await DatabaseSetup.ResetAdminAsync(App.Services, Config(("Setup:AdminUser", "recepcao")));

        // Assert
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        (await CheckPasswordAsync("recepcao", AppFixture.TestPassword)).ShouldBeTrue();
    }

    [Fact]
    public async Task ResetAdmin_DatabaseNotInitialized_ReturnsNotFoundInsteadOfThrowing()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"painel-vazio-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection()
            .AddDbContextFactory<AppDbContext>(options => options.UseSqlite($"Data Source={path}"))
            .AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
            .BuildServiceProvider();

        try
        {
            // Act
            var result = await DatabaseSetup.ResetAdminAsync(services, Config());

            // Assert
            result.Error.Type.ShouldBe(ErrorType.NotFound);
            result.Error.Message.ShouldContain("--initialize");
        }
        finally
        {
            await services.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Initialize_AdminAlreadyExists_DoesNotCreateAnotherOrChangePassword()
    {
        // Act
        var created = await DatabaseSetup.InitializeAsync(App.Services, Config());

        // Assert
        created.ShouldBeNull();
        (await CheckPasswordAsync("admin", AppFixture.TestPassword)).ShouldBeTrue();
    }

    [Fact]
    public async Task SeedTicket_EmptyQueue_IssuesFirstTicketOfFirstDepartment()
    {
        // Act
        var result = await DatabaseSetup.SeedTicketAsync(App.Services);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Code.ShouldBe("T-001");
        result.Value.Status.ShouldBe(TicketStatus.Pending);
    }

    [Fact]
    public async Task SeedTicket_CalledTwice_IssuesOnlyOneTicket()
    {
        // Arrange
        await DatabaseSetup.SeedTicketAsync(App.Services);

        // Act
        var second = await DatabaseSetup.SeedTicketAsync(App.Services);

        // Assert
        second.IsSuccess.ShouldBeTrue();
        second.Value.ShouldBeNull();
        await using var db = await App.CreateDbContextAsync();
        (await db.Tickets.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task SeedTicket_QueueAlreadyHasTickets_DoesNothing()
    {
        // Arrange
        await App.IssueAsync(App.Reception, App.PatrimonyId);

        // Act
        var result = await DatabaseSetup.SeedTicketAsync(App.Services);

        // Assert
        result.Value.ShouldBeNull();
        await using var db = await App.CreateDbContextAsync();
        (await db.Tickets.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public void PasswordGenerator_ManyPasswords_AlwaysMeetThePolicyAndDiffer()
    {
        // Act
        var passwords = Enumerable.Range(0, 200).Select(_ => PasswordGenerator.Generate()).ToList();

        // Assert
        passwords.ShouldAllBe(p => p.Length == 16
            && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit) && p.Any(c => !char.IsLetterOrDigit(c)));
        passwords.Distinct().Count().ShouldBe(passwords.Count);
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build();

    private async Task LockAdminAsync()
    {
        await using var db = await App.CreateDbContextAsync();
        await db.Users.Where(x => x.UserName == "admin")
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.AccessFailedCount, 5).SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddHours(1)));
    }

    private async Task RestoreAdminPasswordAsync()
    {
        var restored = await DatabaseSetup.ResetAdminAsync(App.Services, Config(("Setup:AdminPassword", AppFixture.TestPassword)));
        restored.IsSuccess.ShouldBeTrue();
    }

    private async Task<bool> CheckPasswordAsync(string userName, string password)
    {
        using var scope = App.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByNameAsync(userName);
        return user is not null && await users.CheckPasswordAsync(user, password);
    }
}
