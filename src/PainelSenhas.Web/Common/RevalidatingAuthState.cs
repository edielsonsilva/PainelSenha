using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.EntityFrameworkCore;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Common;

/// <summary>
/// Revalida a sessão do circuito Blazor a cada minuto: usuário inativado ou com senha alterada
/// (novo carimbo de segurança) perde o acesso sem esperar o cookie expirar.
/// </summary>
internal sealed class RevalidatingAuthState(ILoggerFactory logger, IDbContextFactory<AppDbContext> factory)
    : RevalidatingServerAuthenticationStateProvider(logger)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState state, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var id = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = state.User.FindFirstValue("AspNet.Identity.SecurityStamp");
        return await db.Users.AnyAsync(x => x.Id == id && x.Active && x.SecurityStamp == stamp, cancellationToken);
    }
}
