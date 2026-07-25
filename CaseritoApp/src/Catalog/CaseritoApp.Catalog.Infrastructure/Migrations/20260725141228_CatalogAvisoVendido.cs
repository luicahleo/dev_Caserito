using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Catalog.Infrastructure.Migrations;

/// <inheritdoc />
public partial class CatalogAvisoVendido : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "OrdenVentaId",
            schema: "catalog",
            table: "Avisos",
            type: "uniqueidentifier",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OrdenVentaId",
            schema: "catalog",
            table: "Avisos");
    }
}
