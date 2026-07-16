using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KycInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VerificacionesKyc",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificacionesKyc", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesKyc",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReferenciaDocumento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenciaSelfie = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoDocumento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnviadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResueltaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResueltaPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerificacionKycId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesKyc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesKyc_VerificacionesKyc_VerificacionKycId",
                        column: x => x.VerificacionKycId,
                        principalSchema: "identity",
                        principalTable: "VerificacionesKyc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesKyc_VerificacionKycId",
                schema: "identity",
                table: "SolicitudesKyc",
                column: "VerificacionKycId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolicitudesKyc",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "VerificacionesKyc",
                schema: "identity");
        }
    }
}
