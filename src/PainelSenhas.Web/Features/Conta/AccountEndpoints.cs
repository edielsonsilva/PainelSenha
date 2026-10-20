using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using PainelSenhas.Web.Common.Endpoints;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Features.Conta;

/// <summary>
/// Endpoints de formulário da conta: entrar, sair e trocar a própria senha. Cada ação valida o token antiforgery
/// e responde com redirecionamento para a tela; token inválido devolve ProblemDetails 400.
/// </summary>
internal sealed class AccountEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/conta").WithTags("Conta");
        group.MapPost("/entrar", SignInAsync).AllowAnonymous();
        group.MapPost("/sair", SignOutAsync).RequireAuthorization();
        group.MapPost("/senha", ChangePasswordAsync).RequireAuthorization();
    }

    private static async Task<IResult> SignInAsync(
        HttpContext http, IAntiforgery antiforgery, SignInManager<AppUser> signIn, UserManager<AppUser> users)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return InvalidRequest();
        }

        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        var user = await users.FindByNameAsync(form["username"].ToString().Trim());
        if (user is not { Active: true })
        {
            return Results.LocalRedirect("/entrar?erro=1");
        }

        var result = await signIn.PasswordSignInAsync(user, form["password"].ToString(), isPersistent: false, lockoutOnFailure: true);
        return Results.LocalRedirect(result.Succeeded ? "/" : "/entrar?erro=1");
    }

    private static async Task<IResult> SignOutAsync(HttpContext http, IAntiforgery antiforgery, SignInManager<AppUser> signIn)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return InvalidRequest();
        }

        await signIn.SignOutAsync();
        return Results.LocalRedirect("/entrar");
    }

    private static async Task<IResult> ChangePasswordAsync(
        HttpContext http, IAntiforgery antiforgery, SignInManager<AppUser> signIn, UserManager<AppUser> users)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return InvalidRequest();
        }

        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        var current = form["current"].ToString();
        var next = form["new"].ToString();
        if (next != form["confirm"].ToString())
        {
            return Results.LocalRedirect("/conta?erro=confirma");
        }

        var user = await users.GetUserAsync(http.User);
        if (user is not { Active: true })
        {
            return Results.LocalRedirect("/entrar");
        }

        var result = await users.ChangePasswordAsync(user, current, next);
        if (!result.Succeeded)
        {
            var wrongCurrent = result.Errors.Any(x => x.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            return Results.LocalRedirect(wrongCurrent ? "/conta?erro=atual" : "/conta?erro=regra");
        }

        // A troca renova o carimbo de segurança; reemitir o cookie mantém esta sessão válida
        await signIn.RefreshSignInAsync(user);
        return Results.LocalRedirect("/conta?ok=1");
    }

    private static async Task<bool> IsAntiforgeryValidAsync(HttpContext http, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(http);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static IResult InvalidRequest() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Requisição inválida",
        detail: "A página expirou. Recarregue e tente novamente.");
}
