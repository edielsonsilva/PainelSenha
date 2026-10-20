namespace PainelSenhas.Web.Features.Painel;

/// <summary>
/// Totais do dia de todos os departamentos ativos. Fila e atendimento em curso valem para qualquer data (senhas de dias
/// anteriores ainda pendentes contam); os demais valores são da data operacional de hoje. Tempos em minutos.
/// </summary>
public sealed record OverviewTotals(
    int Issued,
    int Pending,
    int InService,
    int Completed,
    int Absent,
    double? AverageWaitMinutes,
    double? AverageServiceMinutes,
    double? LongestWaitMinutes);
