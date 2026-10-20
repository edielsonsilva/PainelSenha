using Microsoft.AspNetCore.Components;

namespace PainelSenhas.Web.Components.Global;

/// <summary>Os três contadores do departamento: fila atual, concluídas e não atendidas na data operacional de hoje.</summary>
public partial class CountersRow
{
    [Parameter]
    public int Pending { get; set; }

    [Parameter]
    public int Completed { get; set; }

    [Parameter]
    public int Absent { get; set; }
}
