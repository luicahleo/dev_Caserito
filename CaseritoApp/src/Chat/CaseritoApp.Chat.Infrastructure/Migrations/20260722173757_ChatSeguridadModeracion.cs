using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Chat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ChatSeguridadModeracion : Migration
{
    private static readonly string[] _columnasBloqueo = ["BloqueadorId", "BloqueadoId"];
    private static readonly string[] _columnasRegistro = ["ReporteId", "CreadoEn"];
    private static readonly string[] _columnasReporte =
        ["ReportanteId", "ConversacionId", "TipoObjetivo", "MensajeId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CerradaEn",
            schema: "chat",
            table: "Conversaciones",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Estado",
            schema: "chat",
            table: "Conversaciones",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<Guid>(
            name: "UltimoActorEstadoId",
            schema: "chat",
            table: "Conversaciones",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "BloqueosUsuario",
            schema: "chat",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BloqueadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BloqueadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BloqueosUsuario", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RegistrosModeracion",
            schema: "chat",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReporteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModeradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Accion = table.Column<int>(type: "int", nullable: false),
                CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegistrosModeracion", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Reportes",
            schema: "chat",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReportanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TipoObjetivo = table.Column<int>(type: "int", nullable: false),
                MensajeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Categoria = table.Column<int>(type: "int", nullable: false),
                Detalle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Estado = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ModeradorAsignadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TomadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ResueltoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reportes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BloqueosUsuario_BloqueadoId",
            schema: "chat",
            table: "BloqueosUsuario",
            column: "BloqueadoId");

        migrationBuilder.CreateIndex(
            name: "IX_BloqueosUsuario_BloqueadorId",
            schema: "chat",
            table: "BloqueosUsuario",
            column: "BloqueadorId");

        migrationBuilder.CreateIndex(
            name: "IX_BloqueosUsuario_BloqueadorId_BloqueadoId",
            schema: "chat",
            table: "BloqueosUsuario",
            columns: _columnasBloqueo,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RegistrosModeracion_ReporteId_CreadoEn",
            schema: "chat",
            table: "RegistrosModeracion",
            columns: _columnasRegistro);

        migrationBuilder.CreateIndex(
            name: "IX_Reportes_ReportanteId_ConversacionId_TipoObjetivo_MensajeId",
            schema: "chat",
            table: "Reportes",
            columns: _columnasReporte,
            unique: true,
            filter: "[Estado] IN (1, 2)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BloqueosUsuario",
            schema: "chat");

        migrationBuilder.DropTable(
            name: "RegistrosModeracion",
            schema: "chat");

        migrationBuilder.DropTable(
            name: "Reportes",
            schema: "chat");

        migrationBuilder.DropColumn(
            name: "CerradaEn",
            schema: "chat",
            table: "Conversaciones");

        migrationBuilder.DropColumn(
            name: "Estado",
            schema: "chat",
            table: "Conversaciones");

        migrationBuilder.DropColumn(
            name: "UltimoActorEstadoId",
            schema: "chat",
            table: "Conversaciones");
    }
}
