namespace PainelSenhas.Web.Features.Gestao;

/// <summary>Usuário da equipe como exibido na gestão (sem dados de credencial), com os departamentos que atende.</summary>
public sealed record UserView(string Id, string UserName, string Role, IReadOnlyList<int> DepartmentIds, bool Active);
