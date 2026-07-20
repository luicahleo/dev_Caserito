using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Código generado por EF usa arrays inline para columnas de índices.

namespace CaseritoApp.Chat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ChatEntregasTiempoReal : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EntregasTiempoReal",
            schema: "chat",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MensajeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Secuencia = table.Column<long>(type: "bigint", nullable: false),
                CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Intentos = table.Column<int>(type: "int", nullable: false),
                ProximoIntentoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcesadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LeaseHasta = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EntregasTiempoReal", x => x.Id);
                table.ForeignKey(
                    name: "FK_EntregasTiempoReal_Mensajes_MensajeId",
                    column: x => x.MensajeId,
                    principalSchema: "chat",
                    principalTable: "Mensajes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EntregasTiempoReal_MensajeId",
            schema: "chat",
            table: "EntregasTiempoReal",
            column: "MensajeId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EntregasTiempoReal_ProcesadaEn_ProximoIntentoEn_LeaseHasta",
            schema: "chat",
            table: "EntregasTiempoReal",
            columns: new[] { "ProcesadaEn", "ProximoIntentoEn", "LeaseHasta" });

        migrationBuilder.CreateIndex(
            name: "IX_EntregasTiempoReal_Secuencia_Id",
            schema: "chat",
            table: "EntregasTiempoReal",
            columns: new[] { "Secuencia", "Id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EntregasTiempoReal",
            schema: "chat");
    }
}
