namespace PainelSenhas.Web.Features.Painel;

/// <summary>
/// Situação de um departamento para a visão geral da gestão: movimento do dia, fila (com as senhas que sobraram de dias
/// anteriores), a senha em atendimento e há quanto tempo, e a espera da senha mais antiga da fila. Tempos em minutos.
/// </summary>
public sealed record DepartmentOverview(
    int Id,
    string Name,
    string Prefix,
    string Desk,
    int Issued,
    int Pending,
    int PendingFromPreviousDays,
    int Completed,
    int Absent,
    string? CurrentCode,
    double? CurrentMinutes,
    double? OldestWaitMinutes,
    double? AverageWaitMinutes,
    double? AverageServiceMinutes);
