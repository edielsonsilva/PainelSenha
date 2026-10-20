using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using PainelSenhas.Web.Common;
using PainelSenhas.Web.Features.Emissao;
using PainelSenhas.Web.Features.Painel;

namespace PainelSenhas.Web.Data;

/// <summary>
/// Operações de linha de comando da instalação: <c>--initialize</c> (migrations, departamentos iniciais e administrador),
/// <c>--reset-admin</c> (nova senha do administrador) e <c>--backup</c> (cópia consistente do banco, SQL Server ou SQLite).
/// Não existe senha padrão: sem <c>Setup:AdminPassword</c>, uma senha aleatória é gerada e exibida uma única vez.
/// </summary>
internal static class DatabaseSetup
{
    private const string DefaultAdminUser = "admin";
    private static readonly Guid SeedTicketKey = new("5eed0001-0000-4000-8000-00000000a001");
    private static readonly string[] SeedNames = ["Tesouraria", "Patrimônio", "Livro Voluntário", "Manutenção Preventiva"];
    private static readonly string[] SeedPrefixes = ["T", "P", "V", "M"];

    /// <summary>Aplica as migrations e cria o que faltar. Devolve as credenciais se o administrador foi criado agora.</summary>
    public static async Task<AdminCredentials?> InitializeAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        }

        if (!await db.Departments.AnyAsync(ct))
        {
            for (var i = 0; i < SeedNames.Length; i++)
            {
                var department = new Department { Name = SeedNames[i], Prefix = SeedPrefixes[i] };
                db.Departments.Add(department);
                await db.SaveChangesAsync(ct);
                db.Desks.Add(new Desk { DepartmentId = department.Id, Name = $"Guichê {i + 1:00}" });
            }

            await db.SaveChangesAsync(ct);
        }

        if (await db.Users.AnyAsync(x => x.AccessRole == Roles.Admin, ct))
        {
            return null;
        }

        return await CreateAdminAsync(scope.ServiceProvider, config);
    }

    /// <summary>
    /// Define uma nova senha para o administrador, destrava a conta e encerra as sessões abertas.
    /// Com mais de um administrador, informe <c>Setup:AdminUser</c>.
    /// </summary>
    public static async Task<Result<AdminCredentials>> ResetAdminAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            return Error.NotFound("O banco ainda não foi inicializado. Rode --initialize.");
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admins = await users.Users.Where(x => x.AccessRole == Roles.Admin).ToListAsync(ct);

        var requested = config["Setup:AdminUser"];
        AppUser admin;
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var match = admins.FirstOrDefault(x => string.Equals(x.UserName, requested.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return Error.NotFound("Não há administrador com o usuário informado.");
            }

            admin = match;
        }
        else if (admins.Count == 1)
        {
            admin = admins[0];
        }
        else if (admins.Count == 0)
        {
            return Error.NotFound("Não há administrador cadastrado. Rode --initialize.");
        }
        else
        {
            return Error.Validation("Há mais de um administrador. Informe o usuário em Setup:AdminUser.");
        }

        var (password, generated) = ResolvePassword(config);

        var token = await users.GeneratePasswordResetTokenAsync(admin);
        var result = await users.ResetPasswordAsync(admin, token, password);
        if (!result.Succeeded)
        {
            return Error.Validation(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        admin.Active = true;
        await users.UpdateAsync(admin);
        await users.SetLockoutEndDateAsync(admin, null);
        await users.ResetAccessFailedCountAsync(admin);
        return new AdminCredentials(admin.UserName ?? "", password, generated);
    }

    /// <summary>
    /// Emite uma senha de exemplo no primeiro departamento ativo, em nome do administrador, para a instalação já abrir com algo na fila.
    /// Só age em banco inicializado e sem nenhuma senha; devolve <c>null</c> quando não há o que semear.
    /// </summary>
    public static async Task<Result<TicketView?>> SeedTicketAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var clock = provider.GetRequiredService<UnitClock>();

        return await provider.GetRequiredService<DatabaseWriter>().ExecuteAsync<TicketView?>(async db =>
        {
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any() || await db.Tickets.AnyAsync(ct))
            {
                return Result<TicketView?>.Success(null);
            }

            var adminId = await db.Users.Where(x => x.AccessRole == Roles.Admin).Select(x => x.Id).FirstOrDefaultAsync(ct);
            var department = await db.Departments.Where(x => x.Active).OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
            if (adminId is null || department is null)
            {
                return Result<TicketView?>.Success(null);
            }

            return Result<TicketView?>.Success(await IssueTicket.CreateAsync(db, clock, department, adminId, SeedTicketKey, preferential: false, ct));
        }, ct);
    }

    /// <summary>
    /// Backup consistente para <paramref name="destination"/>. No SQL Server o arquivo é escrito pelo próprio servidor
    /// (<c>BACKUP DATABASE</c>), então o caminho precisa ser acessível a ele; no LocalDB, qualquer pasta do usuário serve.
    /// </summary>
    public static async Task BackupAsync(DatabaseSettings database, string destination, CancellationToken ct = default)
    {
        var path = Path.GetFullPath(destination);
        if (File.Exists(path))
        {
            throw new InvalidOperationException("O arquivo de destino já existe. Escolha um novo nome.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (database.Provider == DatabaseProvider.SqlServer)
        {
            await using var server = new SqlConnection(database.ConnectionString);
            await server.OpenAsync(ct);
            await using var command = server.CreateCommand();
            command.CommandText = $"BACKUP DATABASE {QuoteName(server.Database)} TO DISK = @path WITH INIT, COPY_ONLY";
            command.CommandTimeout = 0;
            command.Parameters.AddWithValue("@path", path);
            await command.ExecuteNonQueryAsync(ct);
            return;
        }

        await using var source = new SqliteConnection(database.ConnectionString);
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await source.OpenAsync(ct);
        await target.OpenAsync(ct);
        source.BackupDatabase(target);
    }

    private static string QuoteName(string name) => $"[{name.Replace("]", "]]")}]";

    private static async Task<AdminCredentials> CreateAdminAsync(IServiceProvider services, IConfiguration config)
    {
        var username = config["Setup:AdminUser"];
        username = string.IsNullOrWhiteSpace(username) ? DefaultAdminUser : username.Trim();
        var (password, generated) = ResolvePassword(config);

        var users = services.GetRequiredService<UserManager<AppUser>>();
        var result = await users.CreateAsync(new AppUser { UserName = username, AccessRole = Roles.Admin }, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }

        return new AdminCredentials(username, password, generated);
    }

    // Sem Setup:AdminPassword não há senha padrão: gera uma aleatória, que quem chamou exibe uma única vez
    private static (string Password, bool Generated) ResolvePassword(IConfiguration config)
    {
        var configured = config["Setup:AdminPassword"];
        return string.IsNullOrWhiteSpace(configured) ? (PasswordGenerator.Generate(), true) : (configured, false);
    }
}
