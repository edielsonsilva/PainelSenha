using Microsoft.AspNetCore.Components;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Global;

/// <summary>
/// Barra de abas com os departamentos que o usuário pode atender, cada uma com a fila e a senha em atendimento.
/// Só desenha os cabeçalhos: a página mostra o conteúdo logo abaixo e trata a escolha (navegação por URL).
/// </summary>
public partial class DepartmentTabs
{
    [Parameter, EditorRequired]
    public IReadOnlyList<DepartmentView> Departments { get; set; } = [];

    [Parameter]
    public string? SelectedPrefix { get; set; }

    /// <summary>Chamado com o prefixo do departamento escolhido.</summary>
    [Parameter]
    public EventCallback<string> OnSelect { get; set; }

    private int ActiveIndex
    {
        get
        {
            var index = Departments.ToList().FindIndex(x => string.Equals(x.Prefix, SelectedPrefix, StringComparison.OrdinalIgnoreCase));
            return Math.Max(index, 0);
        }
    }

    private Task ChangeAsync(int index) =>
        index >= 0 && index < Departments.Count && !string.Equals(Departments[index].Prefix, SelectedPrefix, StringComparison.OrdinalIgnoreCase)
            ? OnSelect.InvokeAsync(Departments[index].Prefix)
            : Task.CompletedTask;
}
