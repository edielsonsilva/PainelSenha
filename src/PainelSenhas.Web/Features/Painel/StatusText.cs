using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Features.Painel;

/// <summary>Texto em português de cada status de senha, compartilhado pelas telas e pela exportação.</summary>
public static class StatusText
{
    public static string Label(TicketStatus status) => status switch
    {
        TicketStatus.Pending => "Na fila",
        TicketStatus.Called => "Em atendimento",
        TicketStatus.Completed => "Concluída",
        _ => "Ausente"
    };
}
