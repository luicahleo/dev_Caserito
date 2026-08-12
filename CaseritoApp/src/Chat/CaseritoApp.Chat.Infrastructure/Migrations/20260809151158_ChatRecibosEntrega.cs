using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Chat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ChatRecibosEntrega : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "UltimaSecuenciaEntregadaComprador",
            schema: "chat",
            table: "Conversaciones",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "UltimaSecuenciaEntregadaVendedor",
            schema: "chat",
            table: "Conversaciones",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UltimaSecuenciaEntregadaComprador",
            schema: "chat",
            table: "Conversaciones");

        migrationBuilder.DropColumn(
            name: "UltimaSecuenciaEntregadaVendedor",
            schema: "chat",
            table: "Conversaciones");
    }
}
