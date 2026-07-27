using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Notifications.Infrastructure.Migrations;

/// <inheritdoc />
public partial class NotificationsInicial : Migration
{
    private static readonly string[] _columnasIndicePorDestinatarioCreadaId = ["DestinatarioId", "CreatedAt", "Id"];
    private static readonly string[] _columnasIndicePorDestinatarioLeidaCreada = ["DestinatarioId", "IsRead", "CreatedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "notifications");

        migrationBuilder.CreateTable(
            name: "Notifications",
            schema: "notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DestinatarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Body = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsRead = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_DestinatarioId_CreatedAt_Id",
            schema: "notifications",
            table: "Notifications",
            columns: _columnasIndicePorDestinatarioCreadaId);

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_DestinatarioId_IsRead_CreatedAt",
            schema: "notifications",
            table: "Notifications",
            columns: _columnasIndicePorDestinatarioLeidaCreada);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications",
            schema: "notifications");
    }
}
