using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KycArgosScoreSimilitud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ScoreSimilitud",
                schema: "identity",
                table: "SolicitudesKyc",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScoreSimilitud",
                schema: "identity",
                table: "SolicitudesKyc");
        }
    }
}
