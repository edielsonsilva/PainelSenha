using Microsoft.AspNetCore.Components;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Components.Global;
using PainelSenhas.Web.Components.Shared;
using PainelSenhas.Web.Features.Gestao;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Pages.Management;

/// <summary>
/// Visão geral ao vivo da gestão (tela inicial): indicadores de hoje e um cartão por departamento. Atualiza sozinha a cada
/// mudança na fila. A espera da senha mais antiga entra em alerta a partir de <c>Management:LongWaitMinutes</c> (padrão 15).
/// </summary>
public partial class ManagementOverview : LivePage
{
    private OverviewView? _overview;
    private DateTime _updatedAtUtc;

    [Inject]
    private OverviewQuery Query { get; set; } = default!;

    [Inject]
    private UnitClock Clock { get; set; } = default!;

    [Inject]
    private IConfiguration Configuration { get; set; } = default!;

    private double LongWaitMinutes => Configuration.GetValue("Management:LongWaitMinutes", 15d);

    protected override async Task<Result> RefreshAsync()
    {
        var result = await Query.ExecuteAsync(await UserAsync(), StopToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        _overview = result.Value;
        _updatedAtUtc = Clock.UtcNow;
        return Result.Success;
    }
}
