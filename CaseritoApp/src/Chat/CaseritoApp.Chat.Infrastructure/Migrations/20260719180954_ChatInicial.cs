using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Código generado por EF usa arrays inline para columnas de índices.

namespace CaseritoApp.Chat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ChatInicial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "chat");

        migrationBuilder.CreateSequence(
            name: "SecuenciaMensajes",
            schema: "chat");

        migrationBuilder.CreateTable(
            name: "Conversaciones",
            schema: "chat",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AvisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VendedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UltimaActividadEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UltimaSecuencia = table.Column<long>(type: "bigint", nullable: false),
                UltimaSecuenciaLeidaComprador = table.Column<long>(type: "bigint", nullable: false),
                UltimaSecuenciaLeidaVendedor = table.Column<long>(type: "bigint", nullable: false),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conversaciones", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Mensajes",
            schema: "chat",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RemitenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClaveIdempotencia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Secuencia = table.Column<long>(type: "bigint", nullable: false),
                Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                EnviadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Mensajes", x => x.Id);
                table.ForeignKey(
                    name: "FK_Mensajes_Conversaciones_ConversacionId",
                    column: x => x.ConversacionId,
                    principalSchema: "chat",
                    principalTable: "Conversaciones",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Conversaciones_CompradorId_UltimaActividadEn_Id",
            schema: "chat",
            table: "Conversaciones",
            columns: new[] { "CompradorId", "UltimaActividadEn", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_Conversaciones_VendedorId_UltimaActividadEn_Id",
            schema: "chat",
            table: "Conversaciones",
            columns: new[] { "VendedorId", "UltimaActividadEn", "Id" });

        migrationBuilder.CreateIndex(
            name: "UX_Conversaciones_Comprador_Aviso",
            schema: "chat",
            table: "Conversaciones",
            columns: new[] { "CompradorId", "AvisoId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Mensajes_ConversacionId_Secuencia",
            schema: "chat",
            table: "Mensajes",
            columns: new[] { "ConversacionId", "Secuencia" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_Mensajes_Conversacion_Remitente_Clave",
            schema: "chat",
            table: "Mensajes",
            columns: new[] { "ConversacionId", "RemitenteId", "ClaveIdempotencia" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Mensajes",
            schema: "chat");

        migrationBuilder.DropTable(
            name: "Conversaciones",
            schema: "chat");

        migrationBuilder.DropSequence(
            name: "SecuenciaMensajes",
            schema: "chat");
    }
}
#pragma warning restore CA1861
