using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KycIdentidadDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentosKycRegistrados",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HuellaCi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NumeroCiCifrado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ComplementoCiCifrado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DepartamentoExpedicion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosKycRegistrados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosKycRegistrados_HuellaCi",
                schema: "identity",
                table: "DocumentosKycRegistrados",
                column: "HuellaCi",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosKycRegistrados_UsuarioId",
                schema: "identity",
                table: "DocumentosKycRegistrados",
                column: "UsuarioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentosKycRegistrados",
                schema: "identity");
        }
    }
}
