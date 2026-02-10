using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.EntityFramework;

public class WasteManagementContext : DbContext
{
    public DbSet<Place> Places { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<GarbageBinCollectionLine>()
            .HasNoKey()
            .ToView("GarbageBinCollectionsView");
    }
}
