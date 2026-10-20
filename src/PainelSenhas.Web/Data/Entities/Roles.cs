namespace PainelSenhas.Web.Data;

/// <summary>Perfis de acesso do sistema, gravados em <see cref="AppUser.AccessRole"/> e usados em <c>[Authorize(Roles = ...)]</c>.</summary>
public static class Roles
{
    public const string Admin = "Administrador";
    public const string Operator = "Operador";
    public const string Reception = "Recepcao";
}
