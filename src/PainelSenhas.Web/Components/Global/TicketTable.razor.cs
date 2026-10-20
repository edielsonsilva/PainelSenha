using Microsoft.AspNetCore.Components;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Global;

/// <summary>Colunas exibidas por <see cref="TicketTable"/>.</summary>
public enum TicketTableMode
{
    /// <summary>Senhas na fila: senha, hora de emissão e status.</summary>
    Pending,

    /// <summary>Senhas encerradas: senha, status e hora do encerramento.</summary>
    History
}

/// <summary>Tabela compacta de senhas, usada nas listas de fila e de histórico do departamento.</summary>
public partial class TicketTable
{
    [Parameter, EditorRequired]
    public IReadOnlyList<TicketView> Items { get; set; } = [];

    [Parameter]
    public TicketTableMode Mode { get; set; } = TicketTableMode.Pending;

    [Parameter]
    public string EmptyText { get; set; } = "Nenhuma senha.";

    [Inject]
    private UnitClock Clock { get; set; } = default!;

    private string IssuedText(TicketView ticket) =>
        ticket.Date == Clock.Today
            ? Clock.Local(ticket.IssuedUtc).ToString("HH:mm:ss")
            : Clock.Local(ticket.IssuedUtc).ToString("dd/MM HH:mm");

    private string ClosedText(TicketView ticket) =>
        Clock.Local(ticket.ClosedUtc ?? ticket.CalledUtc ?? ticket.IssuedUtc).ToString("dd/MM HH:mm:ss");

    private static string StatusText(TicketStatus status) => status switch
    {
        TicketStatus.Pending => "Na fila",
        TicketStatus.Called => "Em atendimento",
        TicketStatus.Completed => "Concluída",
        _ => "Não atendeu"
    };
}
