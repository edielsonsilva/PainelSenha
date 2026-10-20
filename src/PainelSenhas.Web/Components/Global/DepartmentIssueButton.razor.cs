using Microsoft.AspNetCore.Components;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Global;

/// <summary>
/// Botão grande de um departamento na tela de emissão: um clique emite a senha. Mostra o prefixo, o nome e a fila;
/// durante a emissão exibe o progresso e, logo depois, a confirmação enquanto a tela fica travada contra clique duplo.
/// Cada departamento tem dois botões: o comum e, logo abaixo, o preferencial (<see cref="Preferential"/>).
/// </summary>
public partial class DepartmentIssueButton
{
    [Parameter, EditorRequired]
    public DepartmentView Department { get; set; } = default!;

    /// <summary>Botão da senha preferencial, mais compacto e com a fila só das preferenciais.</summary>
    [Parameter]
    public bool Preferential { get; set; }

    /// <summary>Esta é a emissão em andamento (aguardando o servidor confirmar).</summary>
    [Parameter]
    public bool Issuing { get; set; }

    /// <summary>A senha deste departamento acabou de ser emitida (período de trava).</summary>
    [Parameter]
    public bool JustIssued { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<int> OnIssue { get; set; }
}
