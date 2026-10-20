using System.Security.Claims;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Features.Gestao;

/// <summary>
/// Visão geral ao vivo da gestão (somente administrador): movimento de hoje e situação atual de cada departamento ativo.
/// Lê só as senhas de hoje e as ainda abertas (pendentes ou em atendimento) e agrega em memória, o que se mantém leve
/// para o volume de um dia de atendimento.
/// </summary>
internal sealed class OverviewQuery(IDbContextFactory<AppDbContext> factory, UnitClock clock)
{
    private sealed record Row(
        int DepartmentId,
        DateOnly Date,
        TicketStatus Status,
        string Prefix,
        int Number,
        DateTime IssuedAtUtc,
        DateTime? CalledAtUtc,
        DateTime? ClosedAtUtc);

    public async Task<Result<OverviewView>> ExecuteAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var actor = await Access.RequireAdminAsync(db, principal, ct);
        if (actor.IsFailure)
        {
            return actor.Error;
        }

        var now = clock.UtcNow;
        var today = clock.Today;
        var departments = await (from d in db.Departments.AsNoTracking()
                                 join desk in db.Desks on d.Id equals desk.DepartmentId
                                 where d.Active
                                 orderby d.Id
                                 select new { d.Id, d.Name, d.Prefix, Desk = desk.Name }).ToListAsync(ct);
        var rows = await db.Tickets.AsNoTracking()
            .Where(t => t.BusinessDate == today || t.Status == TicketStatus.Pending || t.Status == TicketStatus.Called)
            .Select(t => new Row(t.DepartmentId, t.BusinessDate, t.Status, t.Prefix, t.Number, t.IssuedAtUtc, t.CalledAtUtc, t.ClosedAtUtc))
            .ToListAsync(ct);

        var byDepartment = departments
            .Select(d => Describe(d.Id, d.Name, d.Prefix, d.Desk, rows.Where(r => r.DepartmentId == d.Id).ToList(), today, now))
            .ToList();
        var activeRows = rows.Where(r => departments.Any(d => d.Id == r.DepartmentId)).ToList();

        var totals = new OverviewTotals(
            Issued: byDepartment.Sum(x => x.Issued),
            Pending: byDepartment.Sum(x => x.Pending),
            InService: byDepartment.Count(x => x.CurrentCode is not null),
            Completed: byDepartment.Sum(x => x.Completed),
            Absent: byDepartment.Sum(x => x.Absent),
            AverageWaitMinutes: Average(WaitTimes(activeRows.Where(r => r.Date == today))),
            AverageServiceMinutes: Average(ServiceTimes(activeRows.Where(r => r.Date == today))),
            LongestWaitMinutes: byDepartment.Max(x => x.OldestWaitMinutes));

        return new OverviewView(today, totals, byDepartment);
    }

    private static DepartmentOverview Describe(int id, string name, string prefix, string desk, List<Row> rows, DateOnly today, DateTime now)
    {
        var todays = rows.Where(r => r.Date == today).ToList();
        var pending = rows.Where(r => r.Status == TicketStatus.Pending).ToList();
        var current = rows.FirstOrDefault(r => r.Status == TicketStatus.Called);

        return new DepartmentOverview(
            id, name, prefix, desk,
            Issued: todays.Count,
            Pending: pending.Count,
            PendingFromPreviousDays: pending.Count(r => r.Date < today),
            Completed: todays.Count(r => r.Status == TicketStatus.Completed),
            Absent: todays.Count(r => r.Status == TicketStatus.Absent),
            CurrentCode: current is null ? null : TicketCode.Format(current.Prefix, current.Number),
            CurrentMinutes: current?.CalledAtUtc is { } calledAt ? (now - calledAt).TotalMinutes : null,
            OldestWaitMinutes: pending.Count == 0 ? null : (now - pending.Min(r => r.IssuedAtUtc)).TotalMinutes,
            AverageWaitMinutes: Average(WaitTimes(todays)),
            AverageServiceMinutes: Average(ServiceTimes(todays)));
    }

    private static IEnumerable<double> WaitTimes(IEnumerable<Row> rows) =>
        rows.Where(r => r.CalledAtUtc is not null).Select(r => (r.CalledAtUtc.GetValueOrDefault() - r.IssuedAtUtc).TotalMinutes);

    private static IEnumerable<double> ServiceTimes(IEnumerable<Row> rows) =>
        rows.Where(r => r.Status == TicketStatus.Completed && r.CalledAtUtc is not null && r.ClosedAtUtc is not null)
            .Select(r => (r.ClosedAtUtc.GetValueOrDefault() - r.CalledAtUtc.GetValueOrDefault()).TotalMinutes);

    private static double? Average(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Average();
    }
}
