namespace PainelSenhas.Web.Data;

/// <summary>Guichê de atendimento. Cada departamento possui exatamente um.</summary>
internal sealed class Desk
{
    public int Id { get; set; }

    public int DepartmentId { get; set; }

    public string Name { get; set; } = "";

    public bool Active { get; set; } = true;
}
