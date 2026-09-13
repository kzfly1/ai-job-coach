using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIJobCoach.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameResumeAnalysisYearsOfExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "YearsOfExperience",
                table: "resume_analysis",
                newName: "years_of_experience");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "years_of_experience",
                table: "resume_analysis",
                newName: "YearsOfExperience");
        }
    }
}
