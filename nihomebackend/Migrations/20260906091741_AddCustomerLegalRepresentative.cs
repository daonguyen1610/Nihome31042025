using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nihomebackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerLegalRepresentative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_customer_contacts_CustomerId",
                table: "customer_contacts");

            migrationBuilder.AddColumn<bool>(
                name: "IsLegalRepresentative",
                table: "customer_contacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalRepresentativeSince",
                table: "customer_contacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                ;WITH ExactMatches AS
                (
                    SELECT contact.Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY contact.CustomerId
                               ORDER BY contact.IsPrimary DESC, contact.Id) AS RowNumber
                    FROM customer_contacts contact
                    INNER JOIN customers customer ON customer.Id = contact.CustomerId
                    WHERE customer.Type = N'Company'
                      AND NULLIF(LTRIM(RTRIM(customer.RepresentativeName)), N'') IS NOT NULL
                      AND LTRIM(RTRIM(contact.FullName)) = LTRIM(RTRIM(customer.RepresentativeName))
                )
                UPDATE contact
                SET IsLegalRepresentative = 1,
                    LegalRepresentativeSince = COALESCE(customer.UpdatedAt, customer.CreatedAt)
                FROM customer_contacts contact
                INNER JOIN ExactMatches candidate
                    ON candidate.Id = contact.Id AND candidate.RowNumber = 1
                INNER JOIN customers customer ON customer.Id = contact.CustomerId;

                INSERT INTO customer_contacts
                    (CustomerId, FullName, Position, Phone, Email, IsPrimary,
                     IsLegalRepresentative, LegalRepresentativeSince, CreatedAt, UpdatedAt)
                SELECT customer.Id,
                       LTRIM(RTRIM(customer.RepresentativeName)),
                       NULL,
                       NULL,
                       NULL,
                       0,
                       1,
                       COALESCE(customer.UpdatedAt, customer.CreatedAt),
                       customer.CreatedAt,
                       customer.UpdatedAt
                FROM customers customer
                WHERE customer.Type = N'Company'
                  AND NULLIF(LTRIM(RTRIM(customer.RepresentativeName)), N'') IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM customer_contacts contact
                      WHERE contact.CustomerId = customer.Id
                        AND contact.IsLegalRepresentative = 1
                  );

                ;WITH PrimaryCandidates AS
                (
                    SELECT contact.Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY contact.CustomerId
                               ORDER BY contact.IsPrimary DESC, contact.Id) AS RowNumber
                    FROM customer_contacts contact
                    INNER JOIN customers customer ON customer.Id = contact.CustomerId
                    WHERE customer.Type = N'Company'
                      AND NOT EXISTS (
                          SELECT 1 FROM customer_contacts existing
                          WHERE existing.CustomerId = customer.Id
                            AND existing.IsLegalRepresentative = 1
                      )
                )
                UPDATE contact
                SET IsLegalRepresentative = 1,
                    LegalRepresentativeSince = COALESCE(customer.UpdatedAt, customer.CreatedAt)
                FROM customer_contacts contact
                INNER JOIN PrimaryCandidates candidate
                    ON candidate.Id = contact.Id AND candidate.RowNumber = 1
                INNER JOIN customers customer ON customer.Id = contact.CustomerId;

                UPDATE customer
                SET RepresentativeName = representative.FullName
                FROM customers customer
                INNER JOIN customer_contacts representative
                    ON representative.CustomerId = customer.Id
                   AND representative.IsLegalRepresentative = 1
                WHERE customer.Type = N'Company';

                IF EXISTS
                (
                    SELECT 1
                    FROM customers customer
                    WHERE customer.Type = N'Company'
                      AND NOT EXISTS (
                          SELECT 1 FROM customer_contacts contact
                          WHERE contact.CustomerId = customer.Id
                            AND contact.IsLegalRepresentative = 1
                      )
                )
                THROW 51000, 'Company customer without a legal representative contact requires manual cleanup.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_customer_contacts_LegalRepresentative",
                table: "customer_contacts",
                column: "CustomerId",
                unique: true,
                filter: "[IsLegalRepresentative] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_customer_contacts_LegalRepresentative",
                table: "customer_contacts");

            migrationBuilder.DropColumn(
                name: "IsLegalRepresentative",
                table: "customer_contacts");

            migrationBuilder.DropColumn(
                name: "LegalRepresentativeSince",
                table: "customer_contacts");

            migrationBuilder.CreateIndex(
                name: "IX_customer_contacts_CustomerId",
                table: "customer_contacts",
                column: "CustomerId");
        }
    }
}
