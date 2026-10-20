using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Components.Global;
using PainelSenhas.Web.Components.Shared;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Emissao;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages;

/// <summary>
/// Tela da recepção: um clique no departamento emite a senha e, por padrão, já imprime o comprovante.
/// Depois de emitir, a tela fica travada por alguns segundos para um clique duplo não gerar duas senhas.
/// A chave de emissão é guardada por departamento e tipo (comum ou preferencial) e só descartada depois de uma emissão
/// confirmada, para que um reenvio após falha de conexão devolva a mesma senha em vez de gerar outra.
/// </summary>
public partial class Emission : LivePage
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(2);

    private readonly Dictionary<(int DepartmentId, bool Preferential), Guid> _issueKeys = [];
    private IReadOnlyList<DepartmentView> _departments = [];
    private (int DepartmentId, bool Preferential)? _issuing;
    private bool _locked;
    private TicketView? _issued;
    private bool _printRequested;
    private IJSObjectReference? _module;

    [Inject]
    private QueueQueries Queries { get; set; } = default!;

    [Inject]
    private IssueTicket Issue { get; set; } = default!;

    [Inject]
    private UnitClock Clock { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private IConfiguration Configuration { get; set; } = default!;

    private string PrintLayout => Configuration["Printing:Layout"] ?? TicketReceipt.Thermal80;

    protected override async Task<Result> RefreshAsync()
    {
        var user = await Queries.CurrentUserAsync(await UserAsync(), StopToken);
        if (user.IsFailure)
        {
            return user.Error;
        }

        if (user.Value.Role is not (Roles.Admin or Roles.Reception))
        {
            return Common.Error.Unauthorized("A emissão é restrita à recepção.");
        }

        _departments = await Queries.DepartmentsAsync(ct: StopToken);
        return Result.Success;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (_printRequested)
        {
            _printRequested = false;
            await PrintAsync();
        }
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // O circuito já foi encerrado; não há módulo a liberar
            }
        }
    }

    private async Task IssueAsync(int departmentId, bool preferential)
    {
        if (Busy || _locked)
        {
            return;
        }

        var target = (departmentId, preferential);
        _issuing = target;
        await RunAsync(async () =>
        {
            if (!_issueKeys.TryGetValue(target, out var key))
            {
                _issueKeys[target] = key = Guid.NewGuid();
            }

            var result = await Issue.ExecuteAsync(await UserAsync(), departmentId, key, preferential, StopToken);
            if (result.IsFailure)
            {
                _issuing = null;
                return result.ToResult();
            }

            _issueKeys.Remove(target);
            _issued = result.Value;
            _printRequested = Configuration.GetValue("Printing:AutoPrint", true);
            _locked = true;
            _ = ReleaseLockAsync();
            return Result.Success;
        });
    }

    private async Task ReleaseLockAsync()
    {
        try
        {
            await Task.Delay(LockDuration, StopToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _locked = false;
            _issuing = null;
            StateHasChanged();
        });
    }

    private async Task PrintAsync()
    {
        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Emission.razor.js");
        await _module.InvokeVoidAsync("print");
    }
}
