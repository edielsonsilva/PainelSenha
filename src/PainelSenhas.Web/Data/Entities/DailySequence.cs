namespace PainelSenhas.Web.Data;

/// <summary>Último número emitido por departamento e data operacional; reinicia a cada dia.</summary>
internal sealed class DailySequence
{
    public int DepartmentId { get; set; }

    public DateOnly BusinessDate { get; set; }

    public int LastNumber { get; set; }
}
