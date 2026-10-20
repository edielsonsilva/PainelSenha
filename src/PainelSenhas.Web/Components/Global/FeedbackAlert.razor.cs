using Microsoft.AspNetCore.Components;

namespace PainelSenhas.Web.Components.Global;

/// <summary>Faixa de erro e de sucesso exibida no topo das telas; não mostra nada quando os textos estão vazios.</summary>
public partial class FeedbackAlert
{
    [Parameter]
    public string? Error { get; set; }

    [Parameter]
    public string? Message { get; set; }
}
