using GameDevManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameDevManager.Data;

public class GameDevManagerDbContext(DbContextOptions<GameDevManagerDbContext> options)
    : DbContext(options)
{
    public DbSet<GameProject> GameProjects => Set<GameProject>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GameProject>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(4000);
        });
    }
}
