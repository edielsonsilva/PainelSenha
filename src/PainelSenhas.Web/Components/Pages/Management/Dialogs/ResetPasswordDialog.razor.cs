using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Features.Gestao;

namespace PainelSenhas.Web.Components.Pages.Management.Dialogs;

/// <summary>
/// Diálogo para definir uma nova senha de um membro da equipe. Pode gerar uma senha segura, que fica visível para o
/// administrador repassá-la. Salva por conta própria e só fecha em caso de sucesso.
/// </summary>
public partial class ResetPasswordDialog
{
    private string _password = "";
    private bool _showPassword;
    private bool _busy;
    private string? _error;

    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public UserView User { get; set; } = default!;

    [Inject]
    private ManageUnit Manage { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider Auth { get; set; } = default!;

    private void Cancel() => MudDialog.Cancel();

    private void GeneratePassword()
    {
        _password = PasswordGenerator.Generate();
        _showPassword = true;
    }

    private async Task SaveAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var principal = (await Auth.GetAuthenticationStateAsync()).User;
            var result = await Manage.ResetPasswordAsync(principal, User.Id, _password);
            if (result.IsSuccess)
            {
                MudDialog.Close(DialogResult.Ok(true));
                return;
            }

            _error = result.Error.Message;
        }
        finally
        {
            _busy = false;
        }
    }
}
