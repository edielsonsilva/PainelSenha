namespace PainelSenhas.Web.Features.Painel;

/// <summary>Tela do guichê: senha em atendimento, próximas senhas da fila e últimas senhas encerradas.</summary>
public sealed record AttendanceView(TicketView? Active, IReadOnlyList<TicketView> Pending, IReadOnlyList<TicketView> Recent);
