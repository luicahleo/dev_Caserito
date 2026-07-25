using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseritoApp.Orders.Infrastructure.Migrations;

/// <inheritdoc />
public partial class OrdersCierrePositivo : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CompletadaEn",
            schema: "orders",
            table: "Orders",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CompradorConfirmoEn",
            schema: "orders",
            table: "Orders",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "MarcadaVendidaEn",
            schema: "orders",
            table: "Orders",
            type: "datetimeoffset",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CompletadaEn",
            schema: "orders",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "CompradorConfirmoEn",
            schema: "orders",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "MarcadaVendidaEn",
            schema: "orders",
            table: "Orders");
    }
}
