using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIJobCoach.Api.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_AddAnalysisAndJobDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_descriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    company = table.Column<string>(type: "text", nullable: true),
                    raw_text = table.Column<string>(type: "text", nullable: false),
                    requirements = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_descriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_descriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resume_analysis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: true),
                    career_level = table.Column<string>(type: "text", nullable: true),
                    programming_languages = table.Column<string>(type: "jsonb", nullable: false),
                    frameworks = table.Column<string>(type: "jsonb", nullable: false),
                    cloud_platforms = table.Column<string>(type: "jsonb", nullable: false),
                    databases = table.Column<string>(type: "jsonb", nullable: false),
                    tools = table.Column<string>(type: "jsonb", nullable: false),
                    projects = table.Column<string>(type: "jsonb", nullable: false),
                    raw_response = table.Column<string>(type: "text", nullable: true),
                    analysed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_analysis", x => x.id);
                    table.ForeignKey(
                        name: "FK_resume_analysis_resumes_resume_id",
                        column: x => x.resume_id,
                        principalTable: "resumes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_descriptions_user_id",
                table: "job_descriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_resume_analysis_resume_id",
                table: "resume_analysis",
                column: "resume_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_descriptions");

            migrationBuilder.DropTable(
                name: "resume_analysis");
        }
    }
}
