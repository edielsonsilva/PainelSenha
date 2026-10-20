using Microsoft.EntityFrameworkCore;

namespace PainelSenhas.Web.Data;

/// <summary>
/// <see cref="AppDbContext"/> para SQL Server. Tem o mesmo modelo; existe como tipo próprio porque o EF liga cada
/// migration ao tipo do contexto, e as migrations do SQL Server não podem se misturar com as do SQLite.
/// O esquema é sempre <c>dbo</c>, explícito, para que os scripts de migration qualifiquem as tabelas como <c>[dbo].[Tabela]</c>.
/// </summary>
internal sealed class SqlServerDbContext(DbContextOptions<SqlServerDbContext> options) : AppDbContext(options)
{
    public const string DefaultSchema = "dbo";

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema(DefaultSchema);
    }
}
