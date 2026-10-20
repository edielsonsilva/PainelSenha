namespace PainelSenhas.Web.Features.Painel;

/// <summary>Uma senha do histórico com quem atendeu, o guichê e os tempos de espera e de atendimento (minutos).</summary>
public sealed record HistoryRow(TicketView Ticket, string? Operator, string? Desk, double? WaitMinutes, double? ServiceMinutes);
