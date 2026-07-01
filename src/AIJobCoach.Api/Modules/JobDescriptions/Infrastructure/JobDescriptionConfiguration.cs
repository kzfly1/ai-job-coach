using AIJobCoach.Api.Modules.Auth.Domain;
using AIJobCoach.Api.Modules.JobDescriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIJobCoach.Api.Modules.JobDescriptions.Infrastructure;

public sealed class JobDescriptionConfiguration : IEntityTypeConfiguration<JobDescription>
{
    public void Configure(EntityTypeBuilder<JobDescription> builder)
    {
        builder.ToTable("job_descriptions");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasColumnType("text").IsRequired();
        builder.Property(x => x.Company).HasColumnName("company").HasColumnType("text");
        builder.Property(x => x.RawText).HasColumnName("raw_text").HasColumnType("text").IsRequired();
        builder.Property(x => x.Requirements).HasColumnName("requirements").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.UserId);
        
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}