using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Código generado por EF usa arrays inline para columnas de índices.

namespace CaseritoApp.Orders.Infrastructure.Migrations;

/// <inheritdoc />
public partial class OrdersInicial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "orders");

        migrationBuilder.CreateTable(
            name: "Orders",
            schema: "orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AvisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VendedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                MontoAcordado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ActualizadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Orders_AvisoId_CompradorId",
            schema: "orders",
            table: "Orders",
            columns: new[] { "AvisoId", "CompradorId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_CompradorId_ActualizadaEn",
            schema: "orders",
            table: "Orders",
            columns: new[] { "CompradorId", "ActualizadaEn" });

        migrationBuilder.CreateIndex(
            name: "IX_Orders_VendedorId_ActualizadaEn",
            schema: "orders",
            table: "Orders",
            columns: new[] { "VendedorId", "ActualizadaEn" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Orders",
            schema: "orders");
    }
}
