namespace PainelSenhas.Web.Data;

/// <summary>Ciclo da senha: Pendente, Chamada e então Concluída ou Ausente. Os valores numéricos são usados no índice parcial do guichê.</summary>
public enum TicketStatus
{
    Pending,
    Called,
    Completed,
    Absent
}
