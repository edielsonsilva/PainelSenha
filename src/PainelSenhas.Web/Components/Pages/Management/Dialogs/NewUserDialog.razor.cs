using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages.Management.Dialogs;

/// <summary>
/// Diálogo para criar um acesso (operador, recepção ou administrador). Oferece gerar uma senha segura, que fica visível para o administrador
/// repassá-la ao usuário. Salva por conta própria e só fecha em caso de sucesso.
/// </summary>
public partial class NewUserDialog
{
    private string _username = "";
    private string _password = "";
    private string _role = Roles.Operator;
    private IReadOnlyCollection<int> _selected = [];
    private bool _showPassword;
    private bool _busy;
    private string? _error;

    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<DepartmentView> Departments { get; set; } = [];

    [Inject]
    private ManageUnit Manage { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider Auth { get; set; } = default!;

    private string DepartmentLabel(int id) => Departments.FirstOrDefault(x => x.Id == id)?.Name ?? id.ToString();

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
            var departments = _role == Roles.Operator ? _selected.ToList() : [];
            var input = new NewUserInput(_username, _password, _role, departments);
            var result = await Manage.CreateUserAsync(principal, input);
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
