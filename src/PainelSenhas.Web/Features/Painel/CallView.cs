namespace PainelSenhas.Web.Features.Painel;

/// <summary>Última chamada de uma senha no painel. <paramref name="EventId"/> evita repetir o aviso sonoro.</summary>
public sealed record CallView(
    long EventId,
    long TicketId,
    string Code,
    string Department,
    string Desk,
    DateTime CalledUtc,
    bool IsRepeat,
    bool IsPreferential = false);
