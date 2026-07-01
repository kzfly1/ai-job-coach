using AIJobCoach.Api.Modules.Resumes.Domain;
using Microsoft.EntityFrameworkCore;

namespace AIJobCoach.Api.Modules.Resumes.Infrastructure;

public sealed class ResumeAnalysisConfiguration : IEntityTypeConfiguration<ResumeAnalysis>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ResumeAnalysis> builder)
    {
        builder.ToTable("resume_analysis");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ResumeId).HasColumnName("resume_id").IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary").HasColumnType("text");
        builder.Property(x => x.CareerLevel).HasColumnName("career_level").HasColumnType("text");
        builder.Property(x => x.ProgrammingLanguages)
            .HasColumnName("programming_languages")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(x => x.Frameworks)
            .HasColumnName("frameworks")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(x => x.CloudPlatforms)
            .HasColumnName("cloud_platforms")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(x => x.Databases)
            .HasColumnName("databases")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(x => x.Tools)
            .HasColumnName("tools")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(x => x.Projects)
            .HasColumnName("projects")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(x => x.RawResponse)
            .HasColumnName("raw_response")
            .HasColumnType("text");
        builder.Property(x => x.AnalysedAt)
            .HasColumnName("analysed_at")
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.ResumeId)
            .IsUnique();
        
        builder.HasOne(x => x.Resume)
            .WithOne(x => x.Analysis)
            .HasForeignKey<ResumeAnalysis>(x => x.ResumeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}