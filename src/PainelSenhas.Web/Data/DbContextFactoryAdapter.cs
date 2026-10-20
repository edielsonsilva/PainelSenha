using Microsoft.EntityFrameworkCore;

namespace PainelSenhas.Web.Data;

/// <summary>
/// Expõe a fábrica de um contexto derivado (<typeparamref name="TContext"/>) como <c>IDbContextFactory&lt;AppDbContext&gt;</c>,
/// que é o que o restante do sistema injeta, independentemente do provedor.
/// </summary>
internal sealed class DbContextFactoryAdapter<TContext>(IDbContextFactory<TContext> inner) : IDbContextFactory<AppDbContext>
    where TContext : AppDbContext
{
    public AppDbContext CreateDbContext() => inner.CreateDbContext();

    public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        await inner.CreateDbContextAsync(cancellationToken);
}
