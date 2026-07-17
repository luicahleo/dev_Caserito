using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KycConcurrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "identity",
                table: "VerificacionesKyc",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                schema: "identity",
                table: "VerificacionesKyc");
        }
    }
}
