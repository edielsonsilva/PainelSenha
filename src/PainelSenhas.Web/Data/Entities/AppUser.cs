using Microsoft.AspNetCore.Identity;

namespace PainelSenhas.Web.Data;

/// <summary>Usuário da equipe. Os departamentos que o operador atende ficam em <see cref="UserDepartment"/>; recepção e administrador não têm vínculo.</summary>
internal sealed class AppUser : IdentityUser
{
    public string AccessRole { get; set; } = Roles.Operator;

    public bool Active { get; set; } = true;
}
