namespace PainelSenhas.Web.Data;

/// <summary>Evento do histórico de uma senha (emissão, chamada, repetição, encerramento).</summary>
internal sealed class TicketEvent
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public EventKind Kind { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string OperatorId { get; set; } = "";

    public int? DeskId { get; set; }

    /// <summary>Chave de idempotência da operação que gerou o evento.</summary>
    public Guid OperationKey { get; set; }
}
