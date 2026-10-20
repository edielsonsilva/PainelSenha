using System.Net;
using System.Text.RegularExpressions;
using Shouldly;

namespace PainelSenhas.Tests.Support;

/// <summary>Auxiliares dos testes HTTP: token antiforgery das páginas, formulários e login por cookie.</summary>
public static partial class TestHttp
{
    /// <summary>Lê o token antiforgery do formulário de uma página renderizada no servidor.</summary>
    public static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path, TestContext.Current.CancellationToken);
        var match = TokenPattern().Match(html);
        match.Success.ShouldBeTrue($"token antiforgery não encontrado em {path}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(x => new KeyValuePair<string, string>(x.Key, x.Value)));

    /// <summary>Cliente HTTP (sem redirecionamento automático) já autenticado como o usuário informado.</summary>
    public static async Task<HttpClient> LoginAsync(this AppFixture app, string userName, string password = AppFixture.TestPassword)
    {
        var client = app.CreateClient();
        var token = await TokenAsync(client, "/entrar");
        var response = await client.PostAsync(
            "/conta/entrar",
            Form(("username", userName), ("password", password), ("__RequestVerificationToken", token)),
            TestContext.Current.CancellationToken);
        response.Headers.Location!.OriginalString.ShouldBe("/");
        return client;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
