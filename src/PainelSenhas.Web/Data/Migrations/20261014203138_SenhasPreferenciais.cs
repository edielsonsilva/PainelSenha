using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelSenhas.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SenhasPreferenciais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_DepartmentId_Status_BusinessDate_Number",
                table: "Tickets");

            migrationBuilder.AddColumn<bool>(
                name: "IsPreferential",
                table: "Tickets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DepartmentId_Status_IsPreferential_BusinessDate_Number",
                table: "Tickets",
                columns: new[] { "DepartmentId", "Status", "IsPreferential", "BusinessDate", "Number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_DepartmentId_Status_IsPreferential_BusinessDate_Number",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsPreferential",
                table: "Tickets");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_DepartmentId_Status_BusinessDate_Number",
                table: "Tickets",
                columns: new[] { "DepartmentId", "Status", "BusinessDate", "Number" });
        }
    }
}
