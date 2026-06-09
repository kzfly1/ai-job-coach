using AIJobCoach.Api.Modules.Auth.Domain;
using AIJobCoach.Api.Modules.Resumes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIJobCoach.Api.Modules.Resumes.Infrastructure;

public sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("resumes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasColumnName("file_name")
            .IsRequired();

        builder.Property(x => x.StoragePath)
            .HasColumnName("storage_path")
            .IsRequired();

        builder.Property(x => x.ContentText)
            .HasColumnName("content_text");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.UploadedAt)
            .HasColumnName("uploaded_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("idx_resumes_user_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}