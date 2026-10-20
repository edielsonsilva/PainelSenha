namespace PainelSenhas.Web.Features.Painel;

/// <summary>
/// Situação de um departamento: fila atual, senhas concluídas e ausentes na data operacional de hoje
/// e o código da senha em atendimento no guichê (nulo quando livre). <paramref name="Pending"/> inclui as
/// <paramref name="PendingPreferential"/> preferenciais aguardando.
/// </summary>
public sealed record DepartmentView(
    int Id,
    string Name,
    string Prefix,
    bool Active,
    string Desk,
    int Pending,
    int Completed,
    int Absent,
    string? CurrentCode,
    int PendingPreferential = 0);
