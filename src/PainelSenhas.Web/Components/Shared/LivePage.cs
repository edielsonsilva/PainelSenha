using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Shared;

/// <summary>
/// Base das telas que acompanham a fila em tempo real. Consulta o banco ao abrir, quando o <see cref="ChangeSignal"/> avisa
/// uma mudança e a cada 5 segundos como reconciliação (recupera avisos perdidos). Concentra o tratamento de erros da tela:
/// falhas esperadas viram mensagem; exceção inesperada é registrada e mostra um texto genérico.
/// </summary>
public abstract class LivePage : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private const int ReconcileTicks = 10;

    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private string? _actionError;
    private string? _refreshError;

    [Inject]
    private ChangeSignal Signal { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider Auth { get; set; } = default!;

    [Inject]
    private ILogger<LivePage> Log { get; set; } = default!;

    /// <summary>Erro da última ação do usuário ou, se não houver, da última atualização de dados.</summary>
    protected string? Error => _actionError ?? _refreshError;

    protected string? Message { get; private set; }

    protected bool Busy { get; private set; }

    protected bool Loading { get; private set; } = true;

    /// <summary>Cancelado quando a tela é descartada; repasse às consultas e operações.</summary>
    protected CancellationToken StopToken => _stop.Token;

    /// <summary>
    /// Se a tela acompanha mudanças em tempo real. Telas de consulta e de cadastro desligam o polling
    /// e só recarregam depois das próprias ações, para não repetir consultas pesadas a cada 5 segundos.
    /// </summary>
    protected virtual bool PollingEnabled => true;

    /// <summary>Recarrega os dados da tela. Falhas esperadas são devolvidas como <see cref="Result"/>.</summary>
    protected abstract Task<Result> RefreshAsync();

    protected async Task<ClaimsPrincipal> UserAsync() => (await Auth.GetAuthenticationStateAsync()).User;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        Loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && PollingEnabled)
        {
            _loop = PollAsync();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    /// <summary>Executa uma ação do usuário com a tela bloqueada; ao terminar, recarrega os dados.</summary>
    protected async Task RunAsync(Func<Task<Result>> action, string? successMessage = null)
    {
        if (Busy)
        {
            return;
        }

        Busy = true;
        _actionError = null;
        Message = null;
        try
        {
            var result = await action();
            if (result.IsFailure)
            {
                _actionError = result.Error.Message;
            }
            else
            {
                Message = successMessage;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Limite da tela: qualquer falha inesperada vira mensagem genérica e fica no log
            Log.LogError(ex, "Falha na operação");
            _actionError = "Não foi possível confirmar a operação. Tente novamente; o pedido será recuperado se já tiver sido salvo.";
        }
        finally
        {
            Busy = false;
            await ReloadAsync();
        }
    }

    protected static string StatusLabel(TicketStatus status) => StatusText.Label(status);

    /// <summary>Libera recursos da tela derivada (por exemplo, módulos JavaScript).</summary>
    protected virtual ValueTask DisposeCoreAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        if (_loop is not null)
        {
            await _loop;
        }

        await DisposeCoreAsync();
        _stop.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(PollInterval);
        var version = Signal.Version;
        var ticks = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                if (++ticks < ReconcileTicks && version == Signal.Version)
                {
                    continue;
                }

                ticks = 0;
                version = Signal.Version;
                await InvokeAsync(async () =>
                {
                    if (!Busy)
                    {
                        await ReloadAsync();
                        StateHasChanged();
                    }
                });
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    private async Task ReloadAsync()
    {
        try
        {
            var result = await RefreshAsync();
            _refreshError = result.IsFailure ? result.Error.Message : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.LogError(ex, "Falha ao atualizar a tela");
            _refreshError = "Não foi possível atualizar os dados. Verifique o servidor e a inicialização do banco.";
        }
    }
}
