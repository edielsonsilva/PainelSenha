namespace PainelSenhas.Web.Data;

/// <summary>Fila de atendimento. O prefixo é único e identifica o departamento no código da senha.</summary>
internal sealed class Department
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Prefix { get; set; } = "";

    public bool Active { get; set; } = true;
}
