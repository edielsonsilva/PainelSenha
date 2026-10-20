using System.Security.Claims;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Features.Emissao;

/// <summary>
/// Emite uma senha comum ou preferencial para um departamento ativo. As duas compartilham a numeração diária, que é
/// incrementada na mesma transação que cria a senha e o evento de emissão; repetir a mesma chave devolve a senha já emitida.
/// </summary>
internal sealed class IssueTicket(DatabaseWriter writer, UnitClock clock)
{
    public Task<Result<TicketView>> ExecuteAsync(
        ClaimsPrincipal principal, int departmentId, Guid key, bool preferential = false, CancellationToken ct = default) =>
        writer.ExecuteAsync<TicketView>(async db =>
        {
            var actor = await Access.RequireReceptionAsync(db, principal, ct);
            if (actor.IsFailure)
            {
                return actor.Error;
            }

            if (key == Guid.Empty)
            {
                return Error.Validation("Identificador de emissão inválido.");
            }

            var previous = await db.Tickets.SingleOrDefaultAsync(x => x.IssueKey == key, ct);
            if (previous is not null)
            {
                if (previous.DepartmentId != departmentId || previous.IsPreferential != preferential || previous.IssuedById != actor.Value.Id)
                {
                    return Error.Conflict("Esta solicitação já foi usada em outra emissão.");
                }

                var previousName = await db.Departments.Where(x => x.Id == previous.DepartmentId).Select(x => x.Name).SingleAsync(ct);
                return QueueQueries.View(previous, previousName);
            }

            var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == departmentId && x.Active, ct);
            if (department is null)
            {
                return Error.NotFound("Departamento indisponível.");
            }

            return await CreateAsync(db, clock, department, actor.Value.Id, key, preferential, ct);
        }, ct);

    /// <summary>Grava a senha, a numeração do dia e o evento de emissão no contexto recebido; quem chama controla a transação e o salvamento final.</summary>
    internal static async Task<TicketView> CreateAsync(AppDbContext db, UnitClock clock, Department department, string issuedById, Guid key, bool preferential, CancellationToken ct)
    {
        var today = clock.Today;
        var sequence = await db.Sequences.FindAsync([department.Id, today], ct);
        if (sequence is null)
        {
            sequence = new DailySequence { DepartmentId = department.Id, BusinessDate = today };
            db.Sequences.Add(sequence);
        }

        var now = clock.UtcNow;
        var ticket = new Ticket
        {
            DepartmentId = department.Id,
            BusinessDate = today,
            Number = ++sequence.LastNumber,
            Prefix = department.Prefix,
            IsPreferential = preferential,
            IssuedAtUtc = now,
            IssueKey = key,
            IssuedById = issuedById
        };
        db.Tickets.Add(ticket);

        // O evento precisa do Id gerado da senha
        await db.SaveChangesAsync(ct);
        db.Events.Add(new TicketEvent
        {
            TicketId = ticket.Id,
            Kind = EventKind.Issued,
            OccurredAtUtc = now,
            OperatorId = issuedById,
            OperationKey = key
        });

        return QueueQueries.View(ticket, department.Name);
    }
}
