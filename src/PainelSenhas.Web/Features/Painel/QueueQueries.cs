using System.Security.Claims;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Features.Painel;

/// <summary>
/// Consultas de leitura usadas pelas quatro telas. Cada chamada abre e descarta o próprio contexto
/// (nunca um contexto compartilhado pela sessão Blazor). Consultas restritas validam o perfil no servidor.
/// </summary>
internal sealed class QueueQueries(IDbContextFactory<AppDbContext> factory, UnitClock clock)
{
    private const int PendingLimit = 50;
    private const int RecentLimit = 8;
    private const int BoardCallLimit = 6;

    /// <summary>Departamentos com fila, concluídas e ausentes do dia e a senha em atendimento. Aberto ao painel público.</summary>
    public async Task<IReadOnlyList<DepartmentView>> DepartmentsAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var today = clock.Today;
        var rows = await (from d in db.Departments.AsNoTracking()
                          join desk in db.Desks on d.Id equals desk.DepartmentId
                          where includeInactive || d.Active
                          orderby d.Id
                          select new
                          {
                              d.Id,
                              d.Name,
                              d.Prefix,
                              d.Active,
                              Desk = desk.Name,
                              Pending = db.Tickets.Count(t => t.DepartmentId == d.Id && t.Status == TicketStatus.Pending),
                              PendingPreferential = db.Tickets.Count(t => t.DepartmentId == d.Id && t.Status == TicketStatus.Pending
                                  && t.IsPreferential),
                              Completed = db.Tickets.Count(t => t.DepartmentId == d.Id && t.BusinessDate == today
                                  && t.Status == TicketStatus.Completed),
                              Absent = db.Tickets.Count(t => t.DepartmentId == d.Id && t.BusinessDate == today
                                  && t.Status == TicketStatus.Absent),
                              CurrentPrefix = db.Tickets.Where(t => t.DepartmentId == d.Id && t.Status == TicketStatus.Called)
                                  .Select(t => t.Prefix).FirstOrDefault(),
                              CurrentNumber = db.Tickets.Where(t => t.DepartmentId == d.Id && t.Status == TicketStatus.Called)
                                  .Select(t => (int?)t.Number).FirstOrDefault()
                          }).ToListAsync(ct);

        return rows.Select(x => new DepartmentView(
            x.Id, x.Name, x.Prefix, x.Active, x.Desk, x.Pending, x.Completed, x.Absent,
            x.CurrentNumber is { } number ? TicketCode.Format(x.CurrentPrefix!, number) : null,
            x.PendingPreferential)).ToList();
    }

    /// <summary>Painel da TV: chamadas recentes agrupadas por senha (repetição não duplica a linha) e resumo por departamento.</summary>
    public async Task<BoardView> BoardAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var events = await (from e in db.Events.AsNoTracking()
                            join t in db.Tickets on e.TicketId equals t.Id
                            join d in db.Departments on t.DepartmentId equals d.Id
                            join desk in db.Desks on e.DeskId equals desk.Id
                            where e.Kind == EventKind.Called || e.Kind == EventKind.Repeated
                            orderby e.Id descending
                            select new
                            {
                                e.Id,
                                e.TicketId,
                                e.Kind,
                                t.Prefix,
                                t.Number,
                                t.IsPreferential,
                                Department = d.Name,
                                Desk = desk.Name,
                                e.OccurredAtUtc
                            }).Take(BoardCallLimit * 10).ToListAsync(ct);

        // Eventos já vêm do mais novo para o mais antigo: o primeiro de cada senha é a chamada mais recente dela
        var calls = events.GroupBy(x => x.TicketId)
            .Select(group => group.First())
            .Take(BoardCallLimit)
            .Select(x => new CallView(x.Id, x.TicketId, TicketCode.Format(x.Prefix, x.Number), x.Department, x.Desk,
                x.OccurredAtUtc, x.Kind == EventKind.Repeated, x.IsPreferential))
            .ToList();

        return new BoardView(calls, await DepartmentsAsync(ct: ct));
    }

    /// <summary>Tela do guichê: exige operador do departamento ou administrador.</summary>
    public async Task<Result<AttendanceView>> AttendanceAsync(ClaimsPrincipal principal, int departmentId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var actor = await Access.RequireAttendantAsync(db, principal, departmentId, ct);
        if (actor.IsFailure)
        {
            return actor.Error;
        }

        var name = await db.Departments.Where(x => x.Id == departmentId).Select(x => x.Name).SingleOrDefaultAsync(ct);
        if (name is null)
        {
            return Error.NotFound("Departamento não encontrado.");
        }

        var open = await db.Tickets.AsNoTracking()
            .Where(x => x.DepartmentId == departmentId && (x.Status == TicketStatus.Pending || x.Status == TicketStatus.Called))
            .OrderByDescending(x => x.Status).ThenByCallPriority()
            .Take(PendingLimit + 1).ToListAsync(ct);
        var recent = await db.Tickets.AsNoTracking()
            .Where(x => x.DepartmentId == departmentId && (x.Status == TicketStatus.Completed || x.Status == TicketStatus.Absent))
            .OrderByDescending(x => x.ClosedAtUtc).Take(RecentLimit).ToListAsync(ct);

        return new AttendanceView(
            open.Where(x => x.Status == TicketStatus.Called).Select(x => View(x, name)).SingleOrDefault(),
            open.Where(x => x.Status == TicketStatus.Pending).Take(PendingLimit).Select(x => View(x, name)).ToList(),
            recent.Select(x => View(x, name)).ToList());
    }

    /// <summary>
    /// Departamentos ativos que o usuário pode atender, na ordem do cadastro: todos para o administrador e,
    /// para o operador, só os vinculados a ele. É o ponto único que decide quais abas do atendimento existem.
    /// </summary>
    public async Task<Result<IReadOnlyList<DepartmentView>>> PermittedDepartmentsAsync(
        ClaimsPrincipal principal, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await Access.RequireAsync(db, principal, ct);
        if (user.IsFailure)
        {
            return user.Error;
        }

        if (user.Value.AccessRole is not (Roles.Admin or Roles.Operator))
        {
            return Error.Unauthorized("Seu perfil não atende guichê.");
        }

        var departments = await DepartmentsAsync(ct: ct);
        if (user.Value.AccessRole == Roles.Admin)
        {
            return departments.ToList();
        }

        var linked = await db.UserDepartments.Where(x => x.UserId == user.Value.Id).Select(x => x.DepartmentId).ToListAsync(ct);
        return departments.Where(x => linked.Contains(x.Id)).ToList();
    }

    public async Task<Result<SessionView>> CurrentUserAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await Access.RequireAsync(db, principal, ct);
        if (user.IsFailure)
        {
            return user.Error;
        }

        return new SessionView(user.Value.Id, user.Value.UserName ?? "", user.Value.AccessRole);
    }

    internal static TicketView View(Ticket t, string department) => new(
        t.Id, TicketCode.Format(t.Prefix, t.Number), department, t.BusinessDate, t.Status,
        t.IssuedAtUtc, t.CalledAtUtc, t.ClosedAtUtc, t.IsPreferential);
}
