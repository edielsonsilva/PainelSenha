using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace PainelSenhas.Web.Components.Global;

/// <summary>
/// Moldura das telas da gestão: título e barra de abas com URL própria (visão geral, histórico, departamentos, equipe).
/// Cada área é uma página; clicar na aba navega para o endereço dela, então cada uma pode ser favoritada.
/// </summary>
public partial class ManagementShell
{
    public const string Overview = "visao-geral";
    public const string History = "historico";
    public const string Departments = "departamentos";
    public const string Team = "equipe";

    private static readonly IReadOnlyList<ManagementArea> Areas =
    [
        new(Overview, "Visão geral", "/gestao", Icons.Material.Filled.Dashboard),
        new(History, "Histórico", "/gestao/historico", Icons.Material.Filled.History),
        new(Departments, "Departamentos", "/gestao/departamentos", Icons.Material.Filled.AccountTree),
        new(Team, "Equipe", "/gestao/equipe", Icons.Material.Filled.Group)
    ];

    /// <summary>Chave da área aberta (<see cref="Overview"/>, <see cref="History"/>, <see cref="Departments"/> ou <see cref="Team"/>).</summary>
    [Parameter, EditorRequired]
    public string Area { get; set; } = Overview;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private int ActiveIndex => Math.Max(0, Areas.ToList().FindIndex(x => x.Key == Area));

    private Task ChangeAsync(int index)
    {
        if (index >= 0 && index < Areas.Count && Areas[index].Key != Area)
        {
            Navigation.NavigateTo(Areas[index].Href);
        }

        return Task.CompletedTask;
    }
}
