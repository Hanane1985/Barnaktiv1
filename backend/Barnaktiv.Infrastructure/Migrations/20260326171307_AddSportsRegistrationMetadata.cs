using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Barnaktiv.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSportsRegistrationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ListingType",
                table: "Activities",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Event");

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationCloseAt",
                table: "Activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationOpenAt",
                table: "Activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationStatus",
                table: "Activities",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "SignupUrl",
                table: "Activities",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sport",
                table: "Activities",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListingType",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RegistrationCloseAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RegistrationOpenAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "RegistrationStatus",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SignupUrl",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Sport",
                table: "Activities");
        }
    }
}
