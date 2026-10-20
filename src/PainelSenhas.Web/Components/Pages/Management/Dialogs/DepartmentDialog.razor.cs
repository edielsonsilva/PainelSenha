using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages.Management.Dialogs;

/// <summary>
/// Diálogo de cadastro de departamento (com o guichê). Salva por conta própria e só fecha em caso de sucesso, para que um erro
/// de validação apareça no próprio diálogo sem perder o que foi digitado.
/// </summary>
public partial class DepartmentDialog
{
    private string _name = "";
    private string _prefix = "";
    private string _deskName = "";
    private bool _active = true;
    private bool _busy;
    private string? _error;

    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>Departamento a editar; nulo cria um novo.</summary>
    [Parameter]
    public DepartmentView? Department { get; set; }

    [Inject]
    private ManageUnit Manage { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider Auth { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (Department is null)
        {
            return;
        }

        _name = Department.Name;
        _prefix = Department.Prefix;
        _deskName = Department.Desk;
        _active = Department.Active;
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task SaveAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var principal = (await Auth.GetAuthenticationStateAsync()).User;
            var input = new DepartmentInput(Department?.Id ?? 0, _name, _prefix, _deskName, _active);
            var result = await Manage.SaveDepartmentAsync(principal, input);
            if (result.IsSuccess)
            {
                MudDialog.Close(DialogResult.Ok(result.Value));
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
