using Microsoft.EntityFrameworkCore;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Preset> Presets => Set<Preset>();
    public DbSet<ScenarioRun> ScenarioRuns => Set<ScenarioRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Preset>()
            .Property(p => p.ApiType)
            .HasConversion<string>();

        modelBuilder.Entity<Preset>()
            .Property(p => p.Environment)
            .HasConversion(
                env => env.ToString(),
                value => EnvironmentTypeHelper.Parse(value));

        modelBuilder.Entity<ScenarioRun>()
            .Property(r => r.ApiType)
            .HasConversion<string>();

        modelBuilder.Entity<ScenarioRun>()
            .Property(r => r.Environment)
            .HasConversion(
                env => env.ToString(),
                value => EnvironmentTypeHelper.Parse(value));

        modelBuilder.Entity<ScenarioRun>()
            .HasIndex(r => r.CreatedAt);

        base.OnModelCreating(modelBuilder);
    }
}
