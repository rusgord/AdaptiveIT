using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaptiveIT.Migrations
{
    /// <inheritdoc />
    public partial class AddPointsAndManualGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresManualGrading",
                table: "TestAttempts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "AwardedPoints",
                table: "StudentAnswers",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyGraded",
                table: "StudentAnswers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TeacherFeedback",
                table: "StudentAnswers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "Questions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresManualGrading",
                table: "TestAttempts");

            migrationBuilder.DropColumn(
                name: "AwardedPoints",
                table: "StudentAnswers");

            migrationBuilder.DropColumn(
                name: "IsManuallyGraded",
                table: "StudentAnswers");

            migrationBuilder.DropColumn(
                name: "TeacherFeedback",
                table: "StudentAnswers");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Questions");
        }
    }
}
