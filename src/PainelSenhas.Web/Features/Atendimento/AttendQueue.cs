using System.Security.Claims;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Features.Atendimento;

/// <summary>
/// Operações do guichê: chamar a próxima senha (preferenciais primeiro, depois FIFO por data e número), repetir a chamada
/// e encerrar como concluída ou ausente. Um guichê só pode ter uma senha chamada por vez, e cada solicitação é idempotente pela chave.
/// </summary>
internal sealed class AttendQueue(DatabaseWriter writer, UnitClock clock)
{
    /// <summary>Chama a próxima senha pendente do departamento (<see cref="TicketQueryExtensions.OrderByCallPriority"/>). Devolve o Id da senha chamada.</summary>
    public Task<Result<long>> CallNextAsync(ClaimsPrincipal principal, int departmentId, Guid key, CancellationToken ct = default) =>
        writer.ExecuteAsync<long>(async db =>
        {
            var actor = await Access.RequireAttendantAsync(db, principal, departmentId, ct);
            if (actor.IsFailure)
            {
                return actor.Error;
            }

            if (key == Guid.Empty)
            {
                return Error.Validation("Identificador da ação inválido.");
            }

            var desk = await db.Desks.SingleOrDefaultAsync(x => x.DepartmentId == departmentId && x.Active, ct);
            if (desk is null)
            {
                return Error.NotFound("Guichê indisponível.");
            }

            var previous = await db.Events.SingleOrDefaultAsync(x => x.OperationKey == key, ct);
            if (previous is not null)
            {
                if (previous.OperatorId != actor.Value.Id || previous.Kind != EventKind.Called || previous.DeskId != desk.Id)
                {
                    return Error.Conflict("Identificador já utilizado por outra ação.");
                }

                return previous.TicketId;
            }

            if (!await db.Departments.AnyAsync(x => x.Id == departmentId && x.Active, ct))
            {
                return Error.NotFound("Departamento inativo.");
            }

            if (await db.Tickets.AnyAsync(x => x.DeskId == desk.Id && x.Status == TicketStatus.Called, ct))
            {
                return Error.Conflict("Encerre a senha atual antes de chamar a próxima.");
            }

            var ticket = await db.Tickets
                .Where(x => x.DepartmentId == departmentId && x.Status == TicketStatus.Pending)
                .OrderByCallPriority()
                .FirstOrDefaultAsync(ct);
            if (ticket is null)
            {
                return Error.NotFound("Não há senhas aguardando neste departamento.");
            }

            ticket.Status = TicketStatus.Called;
            ticket.DeskId = desk.Id;
            ticket.OperatorId = actor.Value.Id;
            ticket.CalledAtUtc = clock.UtcNow;
            db.Events.Add(NewEvent(ticket, actor.Value.Id, key, EventKind.Called));
            return ticket.Id;
        }, ct);

    /// <summary>Repete a chamada ou encerra a senha em atendimento (<see cref="EventKind.Repeated"/>, <see cref="EventKind.Completed"/> ou <see cref="EventKind.Absent"/>).</summary>
    public Task<Result<long>> ActAsync(ClaimsPrincipal principal, long ticketId, EventKind kind, Guid key, CancellationToken ct = default) =>
        writer.ExecuteAsync<long>(async db =>
        {
            if (kind is not (EventKind.Repeated or EventKind.Completed or EventKind.Absent))
            {
                return Error.Validation("Ação inválida.");
            }

            if (key == Guid.Empty)
            {
                return Error.Validation("Identificador da ação inválido.");
            }

            var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId, ct);
            if (ticket is null)
            {
                return Error.NotFound("Senha não encontrada.");
            }

            var actor = await Access.RequireAttendantAsync(db, principal, ticket.DepartmentId, ct);
            if (actor.IsFailure)
            {
                return actor.Error;
            }

            var previous = await db.Events.SingleOrDefaultAsync(x => x.OperationKey == key, ct);
            if (previous is not null)
            {
                if (previous.OperatorId != actor.Value.Id || previous.Kind != kind || previous.TicketId != ticketId)
                {
                    return Error.Conflict("Identificador já utilizado por outra ação.");
                }

                return ticket.Id;
            }

            if (ticket.Status != TicketStatus.Called)
            {
                return Error.Conflict("Esta senha já foi encerrada ou não está em atendimento.");
            }

            if (kind != EventKind.Repeated)
            {
                ticket.Status = kind == EventKind.Completed ? TicketStatus.Completed : TicketStatus.Absent;
                ticket.ClosedAtUtc = clock.UtcNow;
            }

            db.Events.Add(NewEvent(ticket, actor.Value.Id, key, kind));
            return ticket.Id;
        }, ct);

    private TicketEvent NewEvent(Ticket ticket, string actorId, Guid key, EventKind kind) => new()
    {
        TicketId = ticket.Id,
        DeskId = ticket.DeskId,
        OperatorId = actorId,
        OperationKey = key,
        Kind = kind,
        OccurredAtUtc = clock.UtcNow
    };
}
