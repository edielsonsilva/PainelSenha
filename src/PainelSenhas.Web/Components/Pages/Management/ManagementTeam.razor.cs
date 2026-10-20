using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Components.Pages.Management.Dialogs;
using PainelSenhas.Web.Components.Shared;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages.Management;

/// <summary>
/// Acessos da equipe. Criar acesso, redefinir senha e escolher os departamentos do operador acontecem em diálogos;
/// ativar e inativar é direto na linha. Administradores podem ser inativados por outro administrador, nunca pelo próprio
/// nem o último ativo; a senha de administrador troca-se em Minha conta.
/// </summary>
public partial class ManagementTeam : LivePage
{
    private IReadOnlyList<DepartmentView> _departments = [];
    private IReadOnlyList<UserView> _users = [];
    private string? _currentUserId;

    [Inject]
    private QueueQueries Queries { get; set; } = default!;

    [Inject]
    private ManageUnit Manage { get; set; } = default!;

    [Inject]
    private IDialogService Dialogs { get; set; } = default!;

    protected override bool PollingEnabled => false;

    protected override async Task<Result> RefreshAsync()
    {
        _departments = await Queries.DepartmentsAsync(includeInactive: true, ct: StopToken);
        var principal = await UserAsync();
        _currentUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var users = await Manage.UsersAsync(principal, StopToken);
        if (users.IsFailure)
        {
            return users.Error;
        }

        _users = users.Value;
        return Result.Success;
    }

    private static string RoleLabel(string role) => role switch
    {
        Roles.Admin => "Administrador",
        Roles.Operator => "Operador de guichê",
        _ => "Recepção"
    };

    private static Color RoleColor(string role) => role switch
    {
        Roles.Admin => Color.Primary,
        Roles.Operator => Color.Info,
        _ => Color.Secondary
    };

    private string DepartmentNames(IReadOnlyList<int> ids) =>
        ids.Count == 0
            ? "—"
            : string.Join(", ", ids.Select(id => _departments.FirstOrDefault(x => x.Id == id)?.Name ?? "?"));

    private async Task NewUserAsync()
    {
        var parameters = new DialogParameters<NewUserDialog> { { x => x.Departments, _departments } };
        if (await ShowAsync("Novo acesso", parameters))
        {
            await RunAsync(() => Task.FromResult(Result.Success), "Acesso criado. Informe o usuário e a senha ao membro da equipe.");
        }
    }

    private async Task EditDepartmentsAsync(UserView user)
    {
        var parameters = new DialogParameters<UserDepartmentsDialog> { { x => x.User, user }, { x => x.Departments, _departments } };
        if (await ShowAsync($"Departamentos de {user.UserName}", parameters))
        {
            await RunAsync(() => Task.FromResult(Result.Success), "Departamentos atualizados.");
        }
    }

    private async Task ResetPasswordAsync(UserView user)
    {
        var parameters = new DialogParameters<ResetPasswordDialog> { { x => x.User, user } };
        if (await ShowAsync($"Redefinir senha de {user.UserName}", parameters))
        {
            await RunAsync(() => Task.FromResult(Result.Success), "Senha redefinida. Informe a nova senha ao membro da equipe.");
        }
    }

    private Task ToggleAsync(UserView user) => RunAsync(async () =>
        (await Manage.SetUserActiveAsync(await UserAsync(), user.Id, !user.Active, StopToken)).ToResult(),
        user.Active ? "Acesso inativado." : "Acesso ativado.");

    private async Task<bool> ShowAsync<TDialog>(string title, DialogParameters<TDialog> parameters)
        where TDialog : ComponentBase
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var reference = await Dialogs.ShowAsync<TDialog>(title, parameters, options);
        var outcome = await reference.Result;
        return outcome is { Canceled: false };
    }
}
