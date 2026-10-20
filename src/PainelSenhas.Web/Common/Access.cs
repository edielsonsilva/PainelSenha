using System.Security.Claims;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Common;

/// <summary>
/// Autorização no servidor: confirma no banco que o usuário da sessão continua ativo e tem o perfil exigido.
/// Toda operação de escrita e toda consulta restrita passa por aqui, independentemente da tela.
/// </summary>
internal static class Access
{
    public static async Task<Result<AppUser>> RequireAsync(AppDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Active, ct);
        if (principal.Identity?.IsAuthenticated != true || user is null)
        {
            return Error.Unauthorized("Sua sessão não está autorizada. Entre novamente.");
        }

        return user;
    }

    public static async Task<Result<AppUser>> RequireAdminAsync(AppDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var user = await RequireAsync(db, principal, ct);
        if (user.IsSuccess && user.Value.AccessRole != Roles.Admin)
        {
            return Error.Unauthorized("Apenas administradores podem executar esta ação.");
        }

        return user;
    }

    public static async Task<Result<AppUser>> RequireReceptionAsync(AppDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var user = await RequireAsync(db, principal, ct);
        if (user.IsSuccess && user.Value.AccessRole is not (Roles.Admin or Roles.Reception))
        {
            return Error.Unauthorized("A emissão é restrita à recepção.");
        }

        return user;
    }

    /// <summary>Administrador atende qualquer departamento; operador, só os vinculados a ele em <see cref="UserDepartment"/>.</summary>
    public static async Task<Result<AppUser>> RequireAttendantAsync(
        AppDbContext db, ClaimsPrincipal principal, int departmentId, CancellationToken ct)
    {
        var user = await RequireAsync(db, principal, ct);
        if (user.IsFailure || user.Value.AccessRole == Roles.Admin)
        {
            return user;
        }

        var allowed = user.Value.AccessRole == Roles.Operator
            && await db.UserDepartments.AnyAsync(x => x.UserId == user.Value.Id && x.DepartmentId == departmentId, ct);
        if (!allowed)
        {
            return Error.Unauthorized("Você não tem acesso ao atendimento deste departamento.");
        }

        return user;
    }
}
