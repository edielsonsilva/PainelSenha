using System.Net;
using static PainelSenhas.Tests.Support.TestHttp;
using PainelSenhas.Tests.Support;
using PainelSenhas.Web.Data;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class AccountEndpointsTests(AppFixture app) : AppTestBase(app)
{
    [Fact]
    public async Task Login_ValidCredentials_RedirectsHomeAndIssuesCookieOverHttp()
    {
        // Arrange
        var client = App.CreateClient();
        var token = await TokenAsync(client, "/entrar");

        // Act
        var response = await client.PostAsync("/conta/entrar", Form(("username", "admin"), ("password", AppFixture.TestPassword), ("__RequestVerificationToken", token)));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/");
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(".AspNetCore.Identity.Application"));
        cookie.ToLowerInvariant().ShouldNotContain("secure");
        cookie.ToLowerInvariant().ShouldContain("httponly");
    }

    [Fact]
    public async Task Login_WrongPassword_RedirectsBackWithErrorCode()
    {
        // Arrange
        var client = App.CreateClient();
        var token = await TokenAsync(client, "/entrar");

        // Act
        var response = await client.PostAsync("/conta/entrar", Form(("username", "admin"), ("password", "errada"), ("__RequestVerificationToken", token)));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/entrar?erro=1");
    }

    [Fact]
    public async Task Login_InactiveUser_RedirectsBackWithErrorCode()
    {
        // Arrange
        await App.CreateUserAsync("desativado", Roles.Reception);
        await using (var db = await App.CreateDbContextAsync())
        {
            await db.Users.Where(x => x.UserName == "desativado").ExecuteUpdateAsync(x => x.SetProperty(u => u.Active, false));
        }

        var client = App.CreateClient();
        var token = await TokenAsync(client, "/entrar");

        // Act
        var response = await client.PostAsync("/conta/entrar", Form(("username", "desativado"), ("password", AppFixture.TestPassword), ("__RequestVerificationToken", token)));

        // Assert
        response.Headers.Location!.OriginalString.ShouldBe("/entrar?erro=1");
    }

    [Fact]
    public async Task Login_WithoutAntiforgeryToken_ReturnsProblemDetails400()
    {
        // Arrange
        var client = App.CreateClient();

        // Act
        var response = await client.PostAsync("/conta/entrar", Form(("username", "admin"), ("password", AppFixture.TestPassword)));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Health_ReportsHealthyDatabase()
    {
        // Act
        var response = await App.CreateClient().GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Board_Anonymous_IsReachable()
    {
        // Act
        var response = await App.CreateClient().GetAsync("/painel");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/emissao")]
    [InlineData("/atendimento")]
    [InlineData("/atendimento/T")]
    [InlineData("/gestao")]
    [InlineData("/gestao/historico")]
    [InlineData("/gestao/departamentos")]
    [InlineData("/gestao/equipe")]
    [InlineData("/conta")]
    public async Task ProtectedPages_Anonymous_RedirectToLogin(string path)
    {
        // Act
        var response = await App.CreateClient().GetAsync(path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        (location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString).ShouldStartWith("/entrar");
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrent_AllowsLoginWithNewPassword()
    {
        // Arrange
        await App.CreateUserAsync("troca.ok", Roles.Reception);
        var client = await App.LoginAsync("troca.ok", AppFixture.TestPassword);
        var token = await TokenAsync(client, "/conta");

        // Act
        var change = await client.PostAsync("/conta/senha", Form(("current", AppFixture.TestPassword), ("new", "Nova#Senha2026z"), ("confirm", "Nova#Senha2026z"), ("__RequestVerificationToken", token)));
        var relogin = await App.LoginAsync("troca.ok", "Nova#Senha2026z");
        var stillWorks = await relogin.GetAsync("/conta");

        // Assert
        change.Headers.Location!.OriginalString.ShouldBe("/conta?ok=1");
        stillWorks.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_KeepsTheCurrentSessionValid()
    {
        // Arrange
        await App.CreateUserAsync("troca.sessao", Roles.Reception);
        var client = await App.LoginAsync("troca.sessao", AppFixture.TestPassword);
        var token = await TokenAsync(client, "/conta");
        await client.PostAsync("/conta/senha", Form(("current", AppFixture.TestPassword), ("new", "Nova#Senha2026z"), ("confirm", "Nova#Senha2026z"), ("__RequestVerificationToken", token)));

        // Act
        var response = await client.GetAsync("/conta");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("errada", "Nova#Senha2026z", "Nova#Senha2026z", "atual")]
    [InlineData("Senha#Teste2026x", "Nova#Senha2026z", "outra", "confirma")]
    [InlineData("Senha#Teste2026x", "fraca", "fraca", "regra")]
    public async Task ChangePassword_InvalidInput_RedirectsWithErrorCode(string current, string next, string confirm, string code)
    {
        // Arrange
        var user = $"troca.{code}";
        await App.CreateUserAsync(user, Roles.Reception);
        var client = await App.LoginAsync(user, AppFixture.TestPassword);
        var token = await TokenAsync(client, "/conta");

        // Act
        var response = await client.PostAsync("/conta/senha", Form(("current", current), ("new", next), ("confirm", confirm), ("__RequestVerificationToken", token)));

        // Assert
        response.Headers.Location!.OriginalString.ShouldBe($"/conta?erro={code}");
    }
}
