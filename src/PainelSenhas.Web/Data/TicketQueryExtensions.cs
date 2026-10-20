namespace PainelSenhas.Web.Data;

/// <summary>
/// Regra única de ordem de chamada, válida para todos os departamentos: senhas preferenciais primeiro e,
/// dentro de cada grupo, FIFO por data operacional e número. O guichê e a lista "Próximas senhas" usam a mesma ordem.
/// </summary>
internal static class TicketQueryExtensions
{
    public static IOrderedQueryable<Ticket> OrderByCallPriority(this IQueryable<Ticket> tickets) =>
        tickets.OrderByDescending(x => x.IsPreferential).ThenBy(x => x.BusinessDate).ThenBy(x => x.Number);

    public static IOrderedQueryable<Ticket> ThenByCallPriority(this IOrderedQueryable<Ticket> tickets) =>
        tickets.ThenByDescending(x => x.IsPreferential).ThenBy(x => x.BusinessDate).ThenBy(x => x.Number);
}
