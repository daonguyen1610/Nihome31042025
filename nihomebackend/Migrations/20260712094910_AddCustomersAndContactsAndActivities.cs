using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomersAndContactsAndActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RepresentativeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RelationshipStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customers_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "customer_activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_activities_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_activities_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_contacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_contacts_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_activities_CreatedByUserId",
                table: "customer_activities",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_activities_CustomerId",
                table: "customer_activities",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_activities_OccurredAt",
                table: "customer_activities",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_customer_contacts_CustomerId",
                table: "customer_contacts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_contacts_Phone",
                table: "customer_contacts",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_customers_CreatedAt",
                table: "customers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_customers_OwnerUserId",
                table: "customers",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_RelationshipStatus",
                table: "customers",
                column: "RelationshipStatus");

            migrationBuilder.CreateIndex(
                name: "IX_customers_SourceCode",
                table: "customers",
                column: "SourceCode");

            migrationBuilder.CreateIndex(
                name: "IX_customers_TaxId",
                table: "customers",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_Type",
                table: "customers",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_activities");

            migrationBuilder.DropTable(
                name: "customer_contacts");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
