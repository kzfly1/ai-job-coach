using AIJobCoach.Api.Modules.Auth.Domain;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}