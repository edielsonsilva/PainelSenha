using Microsoft.AspNetCore.Components;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Components.Global;
using PainelSenhas.Web.Components.Shared;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Atendimento;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages;

/// <summary>
/// Tela do guichê em <c>/atendimento/{prefixo}</c>. Cada departamento que o usuário pode atender é uma aba com URL própria
/// (o operador de um único departamento não vê abas; o administrador vê todas). Sem prefixo, abre a primeira aba permitida.
/// Cada ação usa uma chave de idempotência que só é descartada após o servidor confirmar, para que um reenvio depois
/// de falha de conexão não duplique chamadas nem encerramentos.
/// </summary>
public partial class Attendance : LivePage
{
    private readonly Dictionary<int, Guid> _callKeys = [];
    private readonly Dictionary<(long TicketId, EventKind Kind), Guid> _actionKeys = [];
    private IReadOnlyList<DepartmentView> _departments = [];
    private DepartmentView? _selected;
    private AttendanceView? _view;
    private string? _loadedPrefix;

    [Parameter]
    public string? Prefix { get; set; }

    [Inject]
    private QueueQueries Queries { get; set; } = default!;

    [Inject]
    private AttendQueue Attend { get; set; } = default!;

    [Inject]
    private UnitClock Clock { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private DepartmentView? Selected => _selected;

    private string? NextCode => _view?.Pending.FirstOrDefault()?.Code;

    protected override async Task<Result> RefreshAsync()
    {
        var principal = await UserAsync();
        var permitted = await Queries.PermittedDepartmentsAsync(principal, StopToken);
        if (permitted.IsFailure)
        {
            return permitted.Error;
        }

        _departments = permitted.Value;
        _selected = null;
        _view = null;
        if (_departments.Count == 0)
        {
            _loadedPrefix = Prefix;
            return Result.Success;
        }

        if (string.IsNullOrWhiteSpace(Prefix))
        {
            _selected = _departments[0];
            Navigation.NavigateTo($"/atendimento/{_selected.Prefix}", replace: true);
        }
        else
        {
            _selected = _departments.FirstOrDefault(x => string.Equals(x.Prefix, Prefix, StringComparison.OrdinalIgnoreCase));
        }

        _loadedPrefix = _selected?.Prefix ?? Prefix;
        if (_selected is null)
        {
            return Result.Success;
        }

        var view = await Queries.AttendanceAsync(principal, _selected.Id, StopToken);
        if (view.IsFailure)
        {
            return view.Error;
        }

        _view = view.Value;
        return Result.Success;
    }

    protected override async Task OnParametersSetAsync()
    {
        // Troca de aba: a rota muda o prefixo e os dados do novo departamento precisam ser carregados
        if (!Loading && !string.Equals(_loadedPrefix, Prefix, StringComparison.OrdinalIgnoreCase))
        {
            await RunAsync(() => Task.FromResult(Result.Success));
        }
    }

    private void SelectDepartment(string prefix) => Navigation.NavigateTo($"/atendimento/{prefix}");

    private Task CallNextAsync() => RunAsync(async () =>
    {
        if (_selected is null)
        {
            return Result.Success;
        }

        if (!_callKeys.TryGetValue(_selected.Id, out var key))
        {
            _callKeys[_selected.Id] = key = Guid.NewGuid();
        }

        var result = await Attend.CallNextAsync(await UserAsync(), _selected.Id, key, StopToken);
        if (result.IsSuccess)
        {
            _callKeys.Remove(_selected.Id);
        }

        return result.ToResult();
    }, "Senha chamada no painel.");

    private Task ActAsync(EventKind kind) => RunAsync(async () =>
    {
        if (_view?.Active is not { } active)
        {
            return Result.Success;
        }

        var target = (active.Id, kind);
        if (!_actionKeys.TryGetValue(target, out var key))
        {
            _actionKeys[target] = key = Guid.NewGuid();
        }

        var result = await Attend.ActAsync(await UserAsync(), active.Id, kind, key, StopToken);
        if (result.IsSuccess)
        {
            _actionKeys.Remove(target);
        }

        return result.ToResult();
    }, kind == EventKind.Repeated ? "Chamada repetida." : "Atendimento encerrado.");
}
