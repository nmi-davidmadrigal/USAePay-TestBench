using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UsaepaySupportTestbench.Data;

#nullable disable

namespace UsaepaySupportTestbench.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260114000100_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Presets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ApiType = table.Column<string>(type: "TEXT", nullable: false),
                Environment = table.Column<string>(type: "TEXT", nullable: false),
                RestMethod = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                RestPathOrEndpoint = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                SoapAction = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                HeadersJson = table.Column<string>(type: "TEXT", nullable: true),
                BodyTemplate = table.Column<string>(type: "TEXT", nullable: true),
                VariablesJson = table.Column<string>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                TagsJson = table.Column<string>(type: "TEXT", nullable: true),
                IsQuickPreset = table.Column<bool>(type: "INTEGER", nullable: false),
                IsSystemPreset = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Presets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ScenarioRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PresetId = table.Column<Guid>(type: "TEXT", nullable: true),
                ApiType = table.Column<string>(type: "TEXT", nullable: false),
                Environment = table.Column<string>(type: "TEXT", nullable: false),
                RequestRedacted = table.Column<string>(type: "TEXT", nullable: false),
                ResponseRedacted = table.Column<string>(type: "TEXT", nullable: false),
                HttpStatus = table.Column<int>(type: "INTEGER", nullable: true),
                SoapFault = table.Column<bool>(type: "INTEGER", nullable: true),
                LatencyMs = table.Column<long>(type: "INTEGER", nullable: false),
                CorrelationId = table.Column<string>(type: "TEXT", nullable: false),
                TicketNumber = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScenarioRuns", x => x.Id);
                table.ForeignKey(
                    name: "FK_ScenarioRuns_Presets_PresetId",
                    column: x => x.PresetId,
                    principalTable: "Presets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ScenarioRuns_CreatedAt",
            table: "ScenarioRuns",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ScenarioRuns_PresetId",
            table: "ScenarioRuns",
            column: "PresetId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ScenarioRuns");

        migrationBuilder.DropTable(
            name: "Presets");
    }
}
