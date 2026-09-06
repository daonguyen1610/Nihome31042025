using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kpi_definitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MetricCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinimumAcceptableScore = table.Column<decimal>(type: "decimal(8,4)", nullable: true),
                    TargetDirection = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "kpi_periods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedByUserId = table.Column<int>(type: "int", nullable: true),
                    LockNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kpi_periods_users_LockedByUserId",
                        column: x => x.LockedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_kpi_periods_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kpi_score_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KpiPeriodId = table.Column<int>(type: "int", nullable: false),
                    KpiDefinitionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    DefinitionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DefinitionNameKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MetricCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefinitionWeight = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinimumAcceptableScore = table.Column<decimal>(type: "decimal(8,4)", nullable: true),
                    TargetDirection = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RawValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Numerator = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Denominator = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Score = table.Column<decimal>(type: "decimal(8,4)", nullable: true),
                    WeightedScore = table.Column<decimal>(type: "decimal(8,4)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_score_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kpi_score_snapshots_kpi_definitions_KpiDefinitionId",
                        column: x => x.KpiDefinitionId,
                        principalTable: "kpi_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_kpi_score_snapshots_kpi_periods_KpiPeriodId",
                        column: x => x.KpiPeriodId,
                        principalTable: "kpi_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kpi_score_snapshots_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_kpi_definitions_Code",
                table: "kpi_definitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kpi_definitions_RoleCode_IsActive",
                table: "kpi_definitions",
                columns: new[] { "RoleCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_kpi_periods_LockedByUserId",
                table: "kpi_periods",
                column: "LockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_kpi_periods_UserId",
                table: "kpi_periods",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_kpi_periods_Year_Month_UserId",
                table: "kpi_periods",
                columns: new[] { "Year", "Month", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kpi_score_snapshots_KpiDefinitionId",
                table: "kpi_score_snapshots",
                column: "KpiDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_kpi_score_snapshots_KpiPeriodId_KpiDefinitionId_UserId",
                table: "kpi_score_snapshots",
                columns: new[] { "KpiPeriodId", "KpiDefinitionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kpi_score_snapshots_UserId_CalculatedAt",
                table: "kpi_score_snapshots",
                columns: new[] { "UserId", "CalculatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kpi_score_snapshots");

            migrationBuilder.DropTable(
                name: "kpi_definitions");

            migrationBuilder.DropTable(
                name: "kpi_periods");
        }
    }
}
