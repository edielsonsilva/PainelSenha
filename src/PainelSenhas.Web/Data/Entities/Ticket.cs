namespace PainelSenhas.Web.Data;

/// <summary>
/// Senha emitida. Guarda o prefixo no momento da emissão para que renomear o departamento não altere o código histórico.
/// </summary>
internal sealed class Ticket
{
    public long Id { get; set; }

    public int DepartmentId { get; set; }

    public DateOnly BusinessDate { get; set; }

    public int Number { get; set; }

    public string Prefix { get; set; } = "";

    /// <summary>Senha preferencial: compartilha a numeração do departamento, mas é sempre chamada antes das comuns.</summary>
    public bool IsPreferential { get; set; }

    public TicketStatus Status { get; set; }

    public int? DeskId { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime? CalledAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? OperatorId { get; set; }

    /// <summary>Chave de idempotência: repetir a mesma solicitação devolve a mesma senha.</summary>
    public Guid IssueKey { get; set; }

    public string IssuedById { get; set; } = "";
}
