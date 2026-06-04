using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaptiveIT.Migrations
{
    /// <inheritdoc />
    public partial class AddTestSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Tests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDateLimit",
                table: "Tests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasTimeLimit",
                table: "Tests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAnswersOnFinish",
                table: "Tests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Tests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMinutes",
                table: "Tests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "HasDateLimit",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "HasTimeLimit",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "ShowAnswersOnFinish",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "TimeLimitMinutes",
                table: "Tests");
        }
    }
}
