using Microsoft.AspNetCore.Components;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Global;

/// <summary>
/// Cartão de um departamento na visão geral da gestão: senha em atendimento, fila, movimento do dia e tempos médios.
/// A espera da senha mais antiga da fila fica em alerta a partir de <see cref="LongWaitMinutes"/>.
/// </summary>
public partial class DepartmentOverviewCard
{
    [Parameter, EditorRequired]
    public DepartmentOverview Data { get; set; } = default!;

    [Parameter]
    public double LongWaitMinutes { get; set; } = 15;

    private bool IsLongWait => Data.OldestWaitMinutes >= LongWaitMinutes;
}
