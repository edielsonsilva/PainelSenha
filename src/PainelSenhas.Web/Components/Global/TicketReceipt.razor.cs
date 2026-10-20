using Microsoft.AspNetCore.Components;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Components.Global;

/// <summary>
/// Comprovante da senha emitida. O layout vem de <c>Printing:Layout</c>: <c>Thermal80</c> (cupom de 80mm, página contínua)
/// ou <c>A4</c> (impressora comum, cartão centralizado). Na impressão, só este bloco aparece (regra global em app.css).
/// </summary>
public partial class TicketReceipt
{
    public const string Thermal80 = "Thermal80";

    [Parameter, EditorRequired]
    public TicketView Ticket { get; set; } = default!;

    [Parameter]
    public string Layout { get; set; } = Thermal80;

    [Inject]
    private UnitClock Clock { get; set; } = default!;

    private bool IsThermal => !string.Equals(Layout, "A4", StringComparison.OrdinalIgnoreCase);

    private string LayoutClass => IsThermal ? "receipt-thermal80" : "receipt-a4";

    private string PageSize => IsThermal ? "80mm auto" : "A4 portrait";

    private string PageMargin => IsThermal ? "0" : "15mm";

    private string IssuedText => Clock.Local(Ticket.IssuedUtc).ToString("dd/MM/yyyy HH:mm");
}
