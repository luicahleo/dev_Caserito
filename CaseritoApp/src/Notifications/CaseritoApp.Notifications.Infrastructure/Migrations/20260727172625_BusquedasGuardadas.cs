using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Notifications.Infrastructure.Migrations;

/// <inheritdoc />
public partial class BusquedasGuardadas : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SavedSearches",
            schema: "notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Keyword = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                MinPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                MaxPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                EstadoProducto = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavedSearches", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SavedSearches_UserId",
            schema: "notifications",
            table: "SavedSearches",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SavedSearches",
            schema: "notifications");
    }
}
