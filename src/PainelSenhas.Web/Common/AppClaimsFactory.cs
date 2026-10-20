using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Common;

/// <summary>Acrescenta o perfil de acesso do usuário como claim de papel no cookie de login.</summary>
internal sealed class AppClaimsFactory(UserManager<AppUser> users, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser>(users, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(ClaimTypes.Role, user.AccessRole));
        return identity;
    }
}
