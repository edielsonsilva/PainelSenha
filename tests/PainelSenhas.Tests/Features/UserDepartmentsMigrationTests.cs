using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PainelSenhas.Web.Data;
using Shouldly;

namespace PainelSenhas.Tests.Features;

public sealed class UserDepartmentsMigrationTests
{
    private const string PreviousMigration = "20261012145900_Initial";

    [Fact]
    public async Task Migrate_OperatorLinkedToOneDepartment_KeepsTheLinkInTheNewTable()
    {
        // Arrange: banco na migration anterior, com um operador vinculado a um departamento
        var path = Path.Combine(Path.GetTempPath(), $"painel-migracao-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;

        try
        {
            await using (var db = new AppDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync(PreviousMigration, TestContext.Current.CancellationToken);
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "Departments" ("Id", "Name", "Prefix", "Active") VALUES (7, 'Secretaria', 'S', 1), (8, 'Outro', 'O', 1);
                    INSERT INTO "AspNetUsers" ("Id", "AccessFailedCount", "AccessRole", "Active", "DepartmentId", "EmailConfirmed", "LockoutEnabled", "PhoneNumberConfirmed", "TwoFactorEnabled")
                    VALUES ('operador-1', 0, 'Operador', 1, 7, 0, 0, 0, 0),
                           ('recepcao-1', 0, 'Recepcao', 1, NULL, 0, 0, 0, 0);
                    """, TestContext.Current.CancellationToken);
            }

            // Act
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            await using var verify = new AppDbContext(options);
            var links = await verify.UserDepartments.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
            links.Select(x => (x.UserId, x.DepartmentId)).ShouldBe([("operador-1", 7)]);
            (await verify.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Rollback_OperatorWithTwoDepartments_KeepsTheLowestDepartmentId()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"painel-reversao-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;

        try
        {
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "Departments" ("Id", "Name", "Prefix", "Active") VALUES (7, 'Secretaria', 'S', 1), (8, 'Outro', 'O', 1);
                    INSERT INTO "AspNetUsers" ("Id", "AccessFailedCount", "AccessRole", "Active", "EmailConfirmed", "LockoutEnabled", "PhoneNumberConfirmed", "TwoFactorEnabled")
                    VALUES ('operador-2', 0, 'Operador', 1, 0, 0, 0, 0);
                    INSERT INTO "UserDepartments" ("UserId", "DepartmentId") VALUES ('operador-2', 8), ('operador-2', 7);
                    """, TestContext.Current.CancellationToken);

                // Act
                await db.GetService<IMigrator>().MigrateAsync(PreviousMigration, TestContext.Current.CancellationToken);
            }

            // Assert
            await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"DepartmentId\" FROM \"AspNetUsers\" WHERE \"Id\" = 'operador-2'";
            (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).ShouldBe(7L);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
