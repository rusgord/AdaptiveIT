using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaptiveIT.Migrations
{
    /// <inheritdoc />
    public partial class AddTestLogicSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdaptive",
                table: "Tests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuestionsPerAttempt",
                table: "Tests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleAnswers",
                table: "Tests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleQuestions",
                table: "Tests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdaptive",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "QuestionsPerAttempt",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "ShuffleAnswers",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "ShuffleQuestions",
                table: "Tests");
        }
    }
}
