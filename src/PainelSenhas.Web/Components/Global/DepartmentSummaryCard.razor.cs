using Microsoft.AspNetCore.Components;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Global;

/// <summary>
/// Cartão informativo de um departamento no painel da TV. <see cref="ShowCurrent"/> mostra a senha em atendimento
/// no guichê e os totais; <see cref="Selected"/> destaca o departamento da última chamada.
/// </summary>
public partial class DepartmentSummaryCard
{
    [Parameter, EditorRequired]
    public DepartmentView Department { get; set; } = default!;

    [Parameter]
    public bool Selected { get; set; }

    [Parameter]
    public bool ShowCurrent { get; set; }

    private string CardClass => Selected ? "pa-3 rounded-lg mud-theme-primary" : "pa-3 rounded-lg";
}
