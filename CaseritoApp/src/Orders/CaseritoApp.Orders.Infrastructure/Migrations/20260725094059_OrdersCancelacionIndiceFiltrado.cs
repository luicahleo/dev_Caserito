using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Código generado por EF usa arrays inline para columnas de índices.

namespace CaseritoApp.Orders.Infrastructure.Migrations;

/// <inheritdoc />
public partial class OrdersCancelacionIndiceFiltrado : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Orders_AvisoId_CompradorId",
            schema: "orders",
            table: "Orders");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_AvisoId_CompradorId",
            schema: "orders",
            table: "Orders",
            columns: new[] { "AvisoId", "CompradorId" },
            unique: true,
            filter: "[Estado] <> 'Cancelled'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Orders_AvisoId_CompradorId",
            schema: "orders",
            table: "Orders");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_AvisoId_CompradorId",
            schema: "orders",
            table: "Orders",
            columns: new[] { "AvisoId", "CompradorId" },
            unique: true);
    }
}
