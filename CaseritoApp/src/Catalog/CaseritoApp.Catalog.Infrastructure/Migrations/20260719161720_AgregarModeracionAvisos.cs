using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarModeracionAvisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstadoModeracion",
                schema: "catalog",
                table: "Avisos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Visible");

            migrationBuilder.CreateTable(
                name: "RegistrosModeracion",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModeradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReporteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosModeracion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportesAviso",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResueltoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesAviso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesAviso_Avisos_AvisoId",
                        column: x => x.AvisoId,
                        principalSchema: "catalog",
                        principalTable: "Avisos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosModeracion_AvisoId_Fecha",
                schema: "catalog",
                table: "RegistrosModeracion",
                columns: new[] { "AvisoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportesAviso_AvisoId_ReportanteId",
                schema: "catalog",
                table: "ReportesAviso",
                columns: new[] { "AvisoId", "ReportanteId" },
                unique: true,
                filter: "[Estado] = N'Pendiente'");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesAviso_Estado_AvisoId",
                schema: "catalog",
                table: "ReportesAviso",
                columns: new[] { "Estado", "AvisoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosModeracion",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ReportesAviso",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "EstadoModeracion",
                schema: "catalog",
                table: "Avisos");
        }
    }
}
