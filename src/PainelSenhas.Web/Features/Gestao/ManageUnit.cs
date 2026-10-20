using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Features.Gestao;

/// <summary>
/// Cadastros da gestão (somente administrador): departamentos com seu guichê, acessos da equipe,
/// ativação/inativação e redefinição de senha. Cadastros com histórico são inativados, nunca excluídos.
/// </summary>
internal sealed class ManageUnit(
    DatabaseWriter writer,
    IDbContextFactory<AppDbContext> factory,
    IServiceScopeFactory scopes,
    IValidator<DepartmentInput> departmentValidator,
    IValidator<NewUserInput> userValidator)
{
    public async Task<Result<int>> SaveDepartmentAsync(ClaimsPrincipal principal, DepartmentInput input, CancellationToken ct = default)
    {
        input = input with { Name = input.Name.Trim(), Prefix = input.Prefix.Trim().ToUpperInvariant(), DeskName = input.DeskName.Trim() };
        var validation = await departmentValidator.ValidateAsync(input, ct);

        return await writer.ExecuteAsync<int>(async db =>
        {
            var actor = await Access.RequireAdminAsync(db, principal, ct);
            if (actor.IsFailure)
            {
                return actor.Error;
            }

            if (!validation.IsValid)
            {
                return Summarize(validation);
            }

            if (await db.Departments.AnyAsync(x => x.Id != input.Id && x.Prefix == input.Prefix, ct))
            {
                return Error.Conflict("Este prefixo já está em uso.");
            }

            Department? department;
            if (input.Id == 0)
            {
                department = new Department();
            }
            else
            {
                department = await db.Departments.FindAsync([input.Id], ct);
                if (department is null)
                {
                    return Error.NotFound("Departamento não encontrado.");
                }
            }

            var hasOpenTickets = input.Id != 0 && await db.Tickets.AnyAsync(
                x => x.DepartmentId == input.Id && (x.Status == TicketStatus.Called || x.Status == TicketStatus.Pending), ct);
            if (!input.Active && hasOpenTickets)
            {
                return Error.Conflict("Encerre as senhas pendentes antes de inativar o departamento.");
            }

            department.Name = input.Name;
            department.Prefix = input.Prefix;
            department.Active = input.Active;
            if (input.Id == 0)
            {
                db.Departments.Add(department);
            }

            // O guichê precisa do Id gerado do departamento
            await db.SaveChangesAsync(ct);
            var desk = await db.Desks.SingleOrDefaultAsync(x => x.DepartmentId == department.Id, ct);
            if (desk is null)
            {
                desk = new Desk { DepartmentId = department.Id };
                db.Desks.Add(desk);
            }

            desk.Name = input.DeskName;
            desk.Active = input.Active;
            return department.Id;
        }, ct);
    }

    public async Task<Result<string>> CreateUserAsync(ClaimsPrincipal principal, NewUserInput input, CancellationToken ct = default)
    {
        input = input with { UserName = input.UserName.Trim() };
        await using var db = await factory.CreateDbContextAsync(ct);
        var actor = await Access.RequireAdminAsync(db, principal, ct);
        if (actor.IsFailure)
        {
            return actor.Error;
        }

        var validation = await userValidator.ValidateAsync(input, ct);
        if (!validation.IsValid)
        {
            return Summarize(validation);
        }

        IReadOnlyList<int> departmentIds = [];
        if (input.Role == Roles.Operator)
        {
            var active = await ActiveDepartmentIdsAsync(db, input.DepartmentIds, ct);
            if (active.IsFailure)
            {
                return active.Error;
            }

            departmentIds = active.Value;
        }

        // UserManager usa um contexto próprio; um escopo novo evita compartilhar estado com a sessão Blazor
        using var scope = scopes.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = input.UserName, AccessRole = input.Role };
        var result = await manager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            return Error.Validation(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        if (departmentIds.Count > 0)
        {
            await writer.ExecuteAsync<bool>(async writerDb =>
            {
                await ReplaceLinksAsync(writerDb, user.Id, departmentIds, ct);
                return true;
            }, ct);
        }

        return user.Id;
    }

    /// <summary>Define os departamentos que um operador atende (substitui a lista anterior). Vale na próxima operação do operador.</summary>
    public Task<Result<bool>> SetUserDepartmentsAsync(
        ClaimsPrincipal principal, string userId, IReadOnlyList<int> departmentIds, CancellationToken ct = default) =>
        writer.ExecuteAsync<bool>(async db =>
        {
            var actor = await Access.RequireAdminAsync(db, principal, ct);
            if (actor.IsFailure)
            {
                return actor.Error;
            }

            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
            if (user is null)
            {
                return Error.NotFound("Usuário não encontrado.");
            }

            if (user.AccessRole != Roles.Operator)
            {
                return Error.Conflict("Somente operadores têm departamentos.");
            }

            if (departmentIds.Count == 0)
            {
                return Error.Validation("Selecione ao menos um departamento para o operador.");
            }

            var active = await ActiveDepartmentIdsAsync(db, departmentIds, ct);
            if (active.IsFailure)
            {
                return active.Error;
            }

            await ReplaceLinksAsync(db, userId, active.Value, ct);
            return true;
        }, ct);

    public async Task<Result<IReadOnlyList<UserView>>> UsersAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var actor = await Access.RequireAdminAsync(db, principal, ct);
        if (actor.IsFailure)
        {
            return actor.Error;
        }

        var users = await db.Users.AsNoTracking().OrderBy(x => x.UserName).ToListAsync(ct);
        var links = (await db.UserDepartments.AsNoTracking().ToListAsync(ct))
            .GroupBy(x => x.UserId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.DepartmentId).Order().ToList());

        return users.Select(x => new UserView(
            x.Id, x.UserName ?? "", x.AccessRole, links.GetValueOrDefault(x.Id) ?? [], x.Active)).ToList();
    }

    public Task<Result<bool>> SetUserActiveAsync(ClaimsPrincipal principal, string userId, bool active, CancellationToken ct = default) =>
        writer.ExecuteAsync<bool>(async db =>
        {
            var actor = await Access.RequireAdminAsync(db, principal, ct);
            if (actor.IsFailure)
            {
                return actor.Error;
            }

            var user = await db.Users.FindAsync([userId], ct);
            if (user is null)
            {
                return Error.NotFound("Usuário não encontrado.");
            }

            if (user.Id == actor.Value.Id)
            {
                return Error.Conflict("Você não pode inativar a própria conta.");
            }

            // Quem inativa é sempre um administrador ativo e não pode ser o alvo (regra acima); como as escritas são
            // serializadas, ele continua ativo depois da operação e o último administrador ativo nunca é inativado.

            user.Active = active;
            // Novo carimbo derruba as sessões abertas do usuário na próxima revalidação
            user.SecurityStamp = Guid.NewGuid().ToString();
            return active;
        }, ct);

    /// <summary>Define uma nova senha para um membro da equipe. Administradores trocam a própria senha em Minha conta.</summary>
    public async Task<Result<bool>> ResetPasswordAsync(ClaimsPrincipal principal, string userId, string newPassword, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var actor = await Access.RequireAdminAsync(db, principal, ct);
        if (actor.IsFailure)
        {
            return actor.Error;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return Error.Validation("Informe a nova senha.");
        }

        using var scope = scopes.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await manager.FindByIdAsync(userId);
        if (user is null)
        {
            return Error.NotFound("Usuário não encontrado.");
        }

        if (user.AccessRole == Roles.Admin)
        {
            return Error.Conflict("A senha de administradores é trocada em Minha conta.");
        }

        var token = await manager.GeneratePasswordResetTokenAsync(user);
        var result = await manager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return Error.Validation(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        await manager.SetLockoutEndDateAsync(user, null);
        await manager.ResetAccessFailedCountAsync(user);
        return true;
    }

    private static async Task<Result<IReadOnlyList<int>>> ActiveDepartmentIdsAsync(
        AppDbContext db, IReadOnlyList<int> requested, CancellationToken ct)
    {
        var ids = requested.Distinct().ToList();
        var active = await db.Departments.CountAsync(x => ids.Contains(x.Id) && x.Active, ct);
        if (ids.Count == 0 || active != ids.Count)
        {
            return Error.Validation("Selecione apenas departamentos ativos.");
        }

        return ids;
    }

    private static async Task ReplaceLinksAsync(AppDbContext db, string userId, IReadOnlyList<int> departmentIds, CancellationToken ct)
    {
        var existing = await db.UserDepartments.Where(x => x.UserId == userId).ToListAsync(ct);
        db.UserDepartments.RemoveRange(existing.Where(x => !departmentIds.Contains(x.DepartmentId)));
        db.UserDepartments.AddRange(departmentIds
            .Where(id => existing.All(x => x.DepartmentId != id))
            .Select(id => new UserDepartment { UserId = userId, DepartmentId = id }));
    }

    private static Error Summarize(ValidationResult validation) =>
        Error.Validation(string.Join(" ", validation.Errors.Select(x => x.ErrorMessage).Distinct()));
}
