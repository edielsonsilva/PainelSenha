using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Components.Shared;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages;

/// <summary>
/// Painel da TV, somente leitura e sem login. Mostra a última chamada, o resumo por departamento e as chamadas anteriores.
/// O aviso sonoro só toca para chamadas novas vistas em atualizações seguidas: depois de uma reconexão longa o painel
/// apenas atualiza o estado, sem anunciar o histórico antigo.
/// </summary>
public partial class Board : LivePage
{
    private static readonly TimeSpan MaxGapForSound = TimeSpan.FromSeconds(15);

    private BoardView _board = new([], []);
    private long? _lastEvent;
    private long? _blinkEventId;
    private DateTime _lastRefresh;
    private bool _sound;
    private IJSObjectReference? _module;

    [Inject]
    private QueueQueries Queries { get; set; } = default!;

    [Inject]
    private UnitClock Clock { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    private CallView? Current => _board.Calls.FirstOrDefault();

    // Só a chamada nova (ou repetida) pisca; ao abrir ou reconectar o painel o card aparece parado
    private string BlinkClass => _blinkEventId is { } id && id == Current?.EventId ? "board-hero-blink" : "";

    protected override async Task<Result> RefreshAsync()
    {
        var next = await Queries.BoardAsync(StopToken);
        var newest = next.Calls.FirstOrDefault()?.EventId ?? 0;
        var now = Clock.UtcNow;
        var isFreshCall = _lastEvent.HasValue && newest > _lastEvent && now - _lastRefresh < MaxGapForSound;

        _board = next;
        _lastEvent = newest;
        _lastRefresh = now;
        if (isFreshCall)
        {
            // O destaque é visual e não depende do som estar ativado
            _blinkEventId = newest;
            if (_sound)
            {
                await (await ModuleAsync()).InvokeVoidAsync("playCall", next.Calls[0].IsPreferential);
            }
        }

        return Result.Success;
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

    private async Task EnableSoundAsync()
    {
        try
        {
            _sound = await InvokeModuleAsync<bool>("enableSound");
        }
        catch (JSException)
        {
            _sound = false;
        }
    }

    private async Task FullscreenAsync()
    {
        try
        {
            await InvokeModuleAsync("fullscreen");
        }
        catch (JSException)
        {
            // Navegador sem suporte a tela cheia: o painel continua funcionando
        }
    }

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Board.razor.js");

    private async Task InvokeModuleAsync(string function) => await (await ModuleAsync()).InvokeVoidAsync(function);

    private async Task<T> InvokeModuleAsync<T>(string function) => await (await ModuleAsync()).InvokeAsync<T>(function);
}
