namespace PainelSenhas.Web.Features.Painel;

/// <summary>Histórico paginado da gestão com totais e médias (em minutos) respeitando os filtros aplicados.</summary>
public sealed record HistoryView(
    IReadOnlyList<HistoryRow> Rows,
    int Total,
    int Pending,
    int Completed,
    int Absent,
    double? WaitMinutes,
    double? ServiceMinutes);
