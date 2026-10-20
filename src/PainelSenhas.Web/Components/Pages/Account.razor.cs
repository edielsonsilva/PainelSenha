using Microsoft.AspNetCore.Components;

namespace PainelSenhas.Web.Components.Pages;

/// <summary>
/// Troca da própria senha. O formulário envia um POST comum para /conta/senha (que também renova o cookie de login);
/// o resultado volta por um código curto na URL, nunca por texto livre.
/// </summary>
public partial class Account
{
    [SupplyParameterFromQuery(Name = "ok")]
    public string? Ok { get; set; }

    [SupplyParameterFromQuery(Name = "erro")]
    public string? Failure { get; set; }

    private string? ErrorMessage => Failure switch
    {
        "atual" => "A senha atual está incorreta.",
        "confirma" => "A confirmação não confere com a nova senha.",
        "regra" => "A nova senha não atende aos requisitos: 10+ caracteres com maiúscula, minúscula, número e símbolo.",
        _ => null
    };
}
