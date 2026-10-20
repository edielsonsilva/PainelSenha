using Microsoft.AspNetCore.Components;

namespace PainelSenhas.Web.Components.Pages;

/// <summary>Página de login renderizada no servidor (sem circuito): o formulário envia um POST comum para /conta/entrar.</summary>
public partial class Login
{
    [SupplyParameterFromQuery(Name = "erro")]
    public string? Error { get; set; }
}
