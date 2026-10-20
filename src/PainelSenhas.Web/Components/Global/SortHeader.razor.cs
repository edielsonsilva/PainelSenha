using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace PainelSenhas.Web.Components.Global;

/// <summary>Cabeçalho de coluna clicável para ordenação no servidor: mostra a seta quando é a coluna ativa.</summary>
public partial class SortHeader
{
    [Parameter, EditorRequired]
    public string Label { get; set; } = "";

    /// <summary>Esta é a coluna pela qual a tabela está ordenada.</summary>
    [Parameter]
    public bool Active { get; set; }

    [Parameter]
    public bool Descending { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    private string? Icon => !Active ? null : Descending ? Icons.Material.Filled.ArrowDownward : Icons.Material.Filled.ArrowUpward;
}
