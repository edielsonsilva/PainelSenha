using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.RegularExpressions;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Features.Gestao;

/// <summary>
/// Histórico de senhas da gestão (somente administrador): consulta paginada com filtros, ordenação no servidor, quem atendeu
/// e os tempos de cada senha, além da lista completa usada na exportação. O período é limitado a 366 dias por consulta.
/// </summary>
internal sealed partial class HistoryQuery(IDbContextFactory<AppDbContext> factory)
{
    public const int MaxPeriodDays = 366;
    public const int ExportLimit = 100_000;

    // Classe com inicializadores de membros (e não um record posicional): o EF só consegue ordenar sobre o resultado
    // de uma projeção quando sabe mapear cada propriedade, o que não vale para argumentos de construtor
    private sealed class Joined
    {
        public required Ticket Ticket { get; init; }

        public required string Department { get; init; }

        public string? Operator { get; init; }

        public string? Desk { get; init; }
    }

    public async Task<Result<HistoryView>> PageAsync(
        ClaimsPrincipal principal, HistoryFilter filter, HistorySort sort, bool descending, int page, int pageSize,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var actor = await Access.RequireAdminAsync(db, principal, ct);
        if (actor.IsFailure)
        {
            return actor.Error;
        }

        var filtered = Filtered(db, filter);
        if (filtered.IsFailure)
        {
            return filtered.Error;
        }

        var query = filtered.Value;
        var counts = await query.GroupBy(t => t.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int CountOf(TicketStatus status) => counts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        double waitSum = 0, serviceSum = 0;
        int waitCount = 0, serviceCount = 0;
        var timings = query.Where(x => x.CalledAtUtc != null)
            .Select(x => new { x.IssuedAtUtc, x.CalledAtUtc, x.ClosedAtUtc, x.Status })
            .AsAsyncEnumerable();
        await foreach (var t in timings.WithCancellation(ct))
        {
            waitSum += (t.CalledAtUtc.GetValueOrDefault() - t.IssuedAtUtc).TotalMinutes;
            waitCount++;
            if (t.Status == TicketStatus.Completed && t.ClosedAtUtc is not null)
            {
                serviceSum += (t.ClosedAtUtc.GetValueOrDefault() - t.CalledAtUtc.GetValueOrDefault()).TotalMinutes;
                serviceCount++;
            }
        }

        var rows = await Order(Join(db, query), sort, descending)
            .Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new HistoryView(
            rows.Select(ToRow).ToList(),
            counts.Sum(x => x.Count),
            CountOf(TicketStatus.Pending),
            CountOf(TicketStatus.Completed),
            CountOf(TicketStatus.Absent),
            waitCount == 0 ? null : waitSum / waitCount,
            serviceCount == 0 ? null : serviceSum / serviceCount);
    }

    /// <summary>Todas as senhas do filtro em ordem cronológica, para exportar. Passar de <see cref="ExportLimit"/> é erro.</summary>
    public async Task<Result<IReadOnlyList<HistoryRow>>> AllAsync(
        ClaimsPrincipal principal, HistoryFilter filter, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var actor = await Access.RequireAdminAsync(db, principal, ct);
        if (actor.IsFailure)
        {
            return actor.Error;
        }

        var filtered = Filtered(db, filter);
        if (filtered.IsFailure)
        {
            return filtered.Error;
        }

        var rows = await Order(Join(db, filtered.Value), HistorySort.Issued, descending: false)
            .Take(ExportLimit + 1).ToListAsync(ct);
        if (rows.Count > ExportLimit)
        {
            return Error.Validation($"O período tem mais de {ExportLimit:N0} senhas. Reduza o intervalo.");
        }

        return rows.Select(ToRow).ToList();
    }

    private static Result<IQueryable<Ticket>> Filtered(AppDbContext db, HistoryFilter filter)
    {
        if (filter.To < filter.From || filter.To.DayNumber - filter.From.DayNumber > MaxPeriodDays)
        {
            return Error.Validation("Selecione um período de até 366 dias.");
        }

        var query = db.Tickets.AsNoTracking().Where(x => x.BusinessDate >= filter.From && x.BusinessDate <= filter.To);
        if (filter.DepartmentId is { } departmentId)
        {
            query = query.Where(x => x.DepartmentId == departmentId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var match = SearchPattern().Match(filter.Search);
            var prefix = match.Groups["prefix"].Success ? match.Groups["prefix"].Value.ToUpperInvariant() : null;
            int? number = match.Groups["number"].Success ? int.Parse(match.Groups["number"].Value) : null;
            if (!match.Success || (prefix is null && number is null))
            {
                return Error.Validation("Busca inválida. Use o código da senha, como T-012, 012 ou só o prefixo T.");
            }

            if (prefix is not null)
            {
                query = query.Where(x => x.Prefix == prefix);
            }

            if (number is { } value)
            {
                query = query.Where(x => x.Number == value);
            }
        }

        return Result<IQueryable<Ticket>>.Success(query);
    }

    private static IQueryable<Joined> Join(AppDbContext db, IQueryable<Ticket> tickets) =>
        from t in tickets
        join d in db.Departments on t.DepartmentId equals d.Id
        join u in db.Users on t.OperatorId equals u.Id into users
        from u in users.DefaultIfEmpty()
        join k in db.Desks on t.DeskId equals k.Id into desks
        from k in desks.DefaultIfEmpty()
        select new Joined
        {
            Ticket = t,
            Department = d.Name,
            Operator = u == null ? null : u.UserName,
            Desk = k == null ? null : k.Name
        };

    // O desempate pelo Id mantém a paginação estável quando a coluna escolhida tem valores repetidos
    private static IOrderedQueryable<Joined> Order(IQueryable<Joined> query, HistorySort sort, bool descending) => sort switch
    {
        HistorySort.Code => Then(Direction(query, x => x.Ticket.Prefix, descending), x => x.Ticket.Number, descending),
        HistorySort.Department => Then(Direction(query, x => x.Department, descending), x => x.Ticket.Id, descending),
        HistorySort.Date => Then(Direction(query, x => x.Ticket.BusinessDate, descending), x => x.Ticket.Number, descending),
        HistorySort.Status => Then(Direction(query, x => x.Ticket.Status, descending), x => x.Ticket.Id, descending),
        HistorySort.Called => Then(Direction(query, x => x.Ticket.CalledAtUtc, descending), x => x.Ticket.Id, descending),
        HistorySort.Closed => Then(Direction(query, x => x.Ticket.ClosedAtUtc, descending), x => x.Ticket.Id, descending),
        _ => Then(Direction(query, x => x.Ticket.IssuedAtUtc, descending), x => x.Ticket.Id, descending)
    };

    private static IOrderedQueryable<Joined> Direction<TKey>(
        IQueryable<Joined> query, Expression<Func<Joined, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    private static IOrderedQueryable<Joined> Then<TKey>(
        IOrderedQueryable<Joined> query, Expression<Func<Joined, TKey>> key, bool descending) =>
        descending ? query.ThenByDescending(key) : query.ThenBy(key);

    private static HistoryRow ToRow(Joined row)
    {
        var ticket = row.Ticket;
        double? wait = ticket.CalledAtUtc is { } called ? (called - ticket.IssuedAtUtc).TotalMinutes : null;
        double? service = ticket.Status == TicketStatus.Completed && ticket.CalledAtUtc is { } start && ticket.ClosedAtUtc is { } end
            ? (end - start).TotalMinutes
            : null;
        return new HistoryRow(QueueQueries.View(ticket, row.Department), row.Operator, row.Desk, wait, service);
    }

    [GeneratedRegex(@"^\s*(?<prefix>[A-Za-z]{1,5})?\s*-?\s*(?<number>\d{1,6})?\s*$")]
    private static partial Regex SearchPattern();
}
