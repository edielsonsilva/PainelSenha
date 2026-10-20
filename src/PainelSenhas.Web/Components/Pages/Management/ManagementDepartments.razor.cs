using Microsoft.AspNetCore.Components;
using MudBlazor;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Components.Pages.Management.Dialogs;
using PainelSenhas.Web.Components.Shared;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages.Management;

/// <summary>
/// Cadastro de departamentos com o guichê de cada um. A criação e a edição acontecem em um diálogo.
/// Departamentos com histórico são inativados, nunca excluídos.
/// </summary>
public partial class ManagementDepartments : LivePage
{
    private IReadOnlyList<DepartmentView> _departments = [];

    [Inject]
    private QueueQueries Queries { get; set; } = default!;

    [Inject]
    private IDialogService Dialogs { get; set; } = default!;

    protected override bool PollingEnabled => false;

    protected override async Task<Result> RefreshAsync()
    {
        var user = await Queries.CurrentUserAsync(await UserAsync(), StopToken);
        if (user.IsFailure)
        {
            return user.Error;
        }

        if (user.Value.Role != Roles.Admin)
        {
            return Common.Error.Unauthorized("Apenas administradores podem executar esta ação.");
        }

        _departments = await Queries.DepartmentsAsync(includeInactive: true, ct: StopToken);
        return Result.Success;
    }

    private async Task OpenAsync(DepartmentView? department)
    {
        var parameters = new DialogParameters<DepartmentDialog> { { x => x.Department, department } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var reference = await Dialogs.ShowAsync<DepartmentDialog>(
            department is null ? "Novo departamento" : "Editar departamento", parameters, options);

        var outcome = await reference.Result;
        if (outcome is { Canceled: false })
        {
            await RunAsync(() => Task.FromResult(Result.Success), "Departamento salvo.");
        }
    }
}
