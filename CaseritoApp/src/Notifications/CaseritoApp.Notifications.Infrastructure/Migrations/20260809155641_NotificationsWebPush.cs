using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Notifications.Infrastructure.Migrations;

/// <inheritdoc />
public partial class NotificationsWebPush : Migration
{
    private static readonly string[] _columnasIntenciones =
        ["ProcesadaEn", "DisponibleEn", "LeaseHasta"];
    private static readonly string[] _columnasSuscripcionesActivas = ["UsuarioId", "Activa"];
    private static readonly string[] _columnasDispositivo = ["UsuarioId", "DispositivoId"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PushIntents",
            schema: "notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DestinatarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Secuencia = table.Column<long>(type: "bigint", nullable: false),
                CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DisponibleEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LeaseHasta = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ProcesadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Intentos = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PushIntents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PushSubscriptions",
            schema: "notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DispositivoId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Endpoint = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                P256dh = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                Auth = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ActualizadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RevocadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Activa = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PushIntents_EventoId",
            schema: "notifications",
            table: "PushIntents",
            column: "EventoId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PushIntents_ProcesadaEn_DisponibleEn_LeaseHasta",
            schema: "notifications",
            table: "PushIntents",
            columns: _columnasIntenciones);

        migrationBuilder.CreateIndex(
            name: "IX_PushSubscriptions_UsuarioId_Activa",
            schema: "notifications",
            table: "PushSubscriptions",
            columns: _columnasSuscripcionesActivas);

        migrationBuilder.CreateIndex(
            name: "IX_PushSubscriptions_UsuarioId_DispositivoId",
            schema: "notifications",
            table: "PushSubscriptions",
            columns: _columnasDispositivo,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PushIntents",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "PushSubscriptions",
            schema: "notifications");
    }
}
