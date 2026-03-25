using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Barnaktiv.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Activities",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "Activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Activities",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RawActivityPayloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawActivityPayloads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_SourceKey_ExternalId",
                table: "Activities",
                columns: new[] { "SourceKey", "ExternalId" },
                unique: true,
                filter: "[SourceKey] <> N'' AND [ExternalId] <> N''");

            migrationBuilder.CreateIndex(
                name: "IX_RawActivityPayloads_SourceKey_ExternalId_ContentHash",
                table: "RawActivityPayloads",
                columns: new[] { "SourceKey", "ExternalId", "ContentHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawActivityPayloads");

            migrationBuilder.DropIndex(
                name: "IX_Activities_SourceKey_ExternalId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Activities");
        }
    }
}
