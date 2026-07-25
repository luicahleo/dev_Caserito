using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Código generado por EF usa arrays inline para columnas de índices.

namespace CaseritoApp.Reputation.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ReputationInicial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "reputation");

        migrationBuilder.CreateTable(
            name: "Reviews",
            schema: "reputation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorRole = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Rating = table.Column<int>(type: "int", nullable: false),
                Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reviews", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Reviews_OrderId_AuthorId",
            schema: "reputation",
            table: "Reviews",
            columns: new[] { "OrderId", "AuthorId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Reviews_OrderId_RecipientId",
            schema: "reputation",
            table: "Reviews",
            columns: new[] { "OrderId", "RecipientId" });

        migrationBuilder.CreateIndex(
            name: "IX_Reviews_RecipientId_CreatedAt_Id",
            schema: "reputation",
            table: "Reviews",
            columns: new[] { "RecipientId", "CreatedAt", "Id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Reviews",
            schema: "reputation");
    }
}
