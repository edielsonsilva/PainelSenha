namespace PainelSenhas.Web.Features.Painel;

/// <summary>Visão geral ao vivo da gestão: data operacional, totais e um bloco por departamento ativo.</summary>
public sealed record OverviewView(DateOnly Date, OverviewTotals Totals, IReadOnlyList<DepartmentOverview> Departments);
