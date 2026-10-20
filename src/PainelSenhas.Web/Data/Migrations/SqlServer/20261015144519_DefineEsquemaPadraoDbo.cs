using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelSenhas.Web.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class DefineEsquemaPadraoDbo : Migration
    {
        // Sem operações de propósito: o SQL Server já criou todas as tabelas em dbo (esquema padrão do usuário).
        // Esta migration só leva o snapshot a declarar dbo explicitamente, e as próximas migrations saem qualificadas.
        // O RenameTable que o EF gera para null -> dbo foi removido por ser, no máximo, ruído.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
