using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages.Management.Dialogs;

/// <summary>Diálogo para escolher os departamentos que um operador atende. Salva por conta própria e só fecha em caso de sucesso.</summary>
public partial class UserDepartmentsDialog
{
    private IReadOnlyCollection<int> _selected = [];
    private bool _busy;
    private string? _error;

    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public UserView User { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<DepartmentView> Departments { get; set; } = [];

    [Inject]
    private ManageUnit Manage { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider Auth { get; set; } = default!;

    protected override void OnInitialized() => _selected = User.DepartmentIds.ToList();

    private string DepartmentLabel(int id) => Departments.FirstOrDefault(x => x.Id == id)?.Name ?? id.ToString();

    private void Cancel() => MudDialog.Cancel();

    private async Task SaveAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var principal = (await Auth.GetAuthenticationStateAsync()).User;
            var result = await Manage.SetUserDepartmentsAsync(principal, User.Id, _selected.ToList());
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
