using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddContactMessageLeadLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadId",
                table: "contact_messages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_messages_LeadId",
                table: "contact_messages",
                column: "LeadId");

            migrationBuilder.AddForeignKey(
                name: "FK_contact_messages_leads_LeadId",
                table: "contact_messages",
                column: "LeadId",
                principalTable: "leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contact_messages_leads_LeadId",
                table: "contact_messages");

            migrationBuilder.DropIndex(
                name: "IX_contact_messages_LeadId",
                table: "contact_messages");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "contact_messages");
        }
    }
}
