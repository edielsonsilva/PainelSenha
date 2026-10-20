using Microsoft.AspNetCore.Components;

namespace PainelSenhas.Web.Components.Global;

/// <summary>Cartão de indicador: rótulo, valor em destaque e uma legenda opcional. <see cref="Alert"/> destaca valores que pedem atenção.</summary>
public partial class KpiTile
{
    [Parameter, EditorRequired]
    public string Label { get; set; } = "";

    [Parameter, EditorRequired]
    public string Value { get; set; } = "";

    [Parameter]
    public string? Caption { get; set; }

    [Parameter]
    public bool Alert { get; set; }
}
