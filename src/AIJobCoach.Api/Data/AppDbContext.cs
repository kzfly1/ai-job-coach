using AIJobCoach.Api.Modules.Auth.Domain;
using AIJobCoach.Api.Modules.JobDescriptions.Domain;
using AIJobCoach.Api.Modules.Resumes.Domain;
using Microsoft.EntityFrameworkCore;

namespace AIJobCoach.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<ResumeAnalysis> ResumeAnalysis => Set<ResumeAnalysis>();
    public DbSet<JobDescription> JobDescriptions => Set<JobDescription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}