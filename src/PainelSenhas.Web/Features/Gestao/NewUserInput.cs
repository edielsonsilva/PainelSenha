namespace PainelSenhas.Web.Features.Gestao;

/// <summary>
/// Dados para criar um acesso. A senha segue a política do ASP.NET Identity (10+ caracteres, maiúscula, minúscula, número, símbolo).
/// <paramref name="DepartmentIds"/> só vale para o operador, que precisa de ao menos um departamento; administrador e recepção
/// não têm vínculo com departamentos (o administrador atende qualquer um).
/// </summary>
public sealed record NewUserInput(string UserName, string Password, string Role, IReadOnlyList<int> DepartmentIds);
