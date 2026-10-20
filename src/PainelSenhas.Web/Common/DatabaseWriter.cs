using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PainelSenhas.Web.Data;

namespace PainelSenhas.Web.Common;

/// <summary>
/// Executa uma operação de escrita em uma transação curta que já reserva o escritor do banco antes de ler sequência
/// ou fila: <c>BEGIN IMMEDIATE</c> no SQLite e um bloqueio de aplicação (<c>sp_getapplock</c>) no SQL Server.
/// Só grava e avisa as telas se a operação terminar com sucesso.
/// </summary>
internal sealed class DatabaseWriter(IDbContextFactory<AppDbContext> factory, ChangeSignal signal)
{
    private const int MaxRetries = 2;
    private const int SqlServerLockTimeoutMs = 5000;

    // Mensagem lançada pelo THROW de AcquireWriterLockAsync quando o bloqueio não é obtido a tempo
    private const int SqlServerLockBusyError = 51000;

    public async Task<Result<T>> ExecuteAsync<T>(Func<AppDbContext, Task<Result<T>>> operation, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                await db.Database.OpenConnectionAsync(ct);
                await using var transaction = await BeginWriterTransactionAsync(db, ct);

                var result = await operation(db);
                if (result.IsFailure)
                {
                    return result;
                }

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                signal.Publish();
                return result;
            }
            catch (Exception ex) when (IsBusy(ex))
            {
                if (attempt >= MaxRetries)
                {
                    return Error.Conflict("O banco está ocupado. Tente novamente em alguns segundos.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), ct);
            }
        }
    }

    private static async Task<DbTransaction> BeginWriterTransactionAsync(AppDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (db.Database.IsSqlite())
        {
            var sqlite = ((SqliteConnection)connection).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(sqlite, ct);
            return sqlite;
        }

        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.UseTransactionAsync(transaction, ct);
        await AcquireWriterLockAsync(db, ct);
        return transaction;
    }

    // Serializa os escritores como o SQLite faz: a numeração diária e a escolha da próxima senha leem e gravam
    // a mesma linha, e sem o bloqueio duas transações concorrentes leriam o mesmo número. O bloqueio é solto no fim da transação.
    private static Task AcquireWriterLockAsync(AppDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync(
            $"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = N'PainelSenhas.Writer', @LockMode = N'Exclusive',
                 @LockOwner = N'Transaction', @LockTimeout = {SqlServerLockTimeoutMs};
            IF @result < 0 THROW {SqlServerLockBusyError}, N'Banco ocupado.', 1;
            """, ct);

    // SQLite: SQLITE_BUSY (5) e SQLITE_LOCKED (6). SQL Server: deadlock (1205), timeout de bloqueio (1222) e o THROW do bloqueio de escrita
    private static bool IsBusy(Exception ex) =>
        IsBusyCode(ex) || (ex.InnerException is { } inner && IsBusyCode(inner));

    private static bool IsBusyCode(Exception ex) => ex switch
    {
        SqliteException { SqliteErrorCode: 5 or 6 } => true,
        SqlException { Number: 1205 or 1222 or SqlServerLockBusyError } => true,
        _ => false
    };
}
