using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Notifications.Infrastructure.Migrations;

/// <inheritdoc />
public partial class PuntosEncuentroSeguros : Migration
{
    private static readonly string[] _columnasIndiceCiudadActivo = ["City", "IsActive"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SafeMeetingPoints",
            schema: "notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SafeMeetingPoints", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SafeMeetingPoints_City_IsActive",
            schema: "notifications",
            table: "SafeMeetingPoints",
            columns: _columnasIndiceCiudadActivo);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SafeMeetingPoints",
            schema: "notifications");
    }
}
