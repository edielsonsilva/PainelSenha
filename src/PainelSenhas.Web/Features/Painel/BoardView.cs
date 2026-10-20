namespace PainelSenhas.Web.Features.Painel;

/// <summary>Conteúdo do painel da TV: chamadas mais recentes (uma por senha, a mais nova primeiro) e resumo por departamento.</summary>
public sealed record BoardView(IReadOnlyList<CallView> Calls, IReadOnlyList<DepartmentView> Departments);
