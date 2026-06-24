using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.EntityFramework;

public class WasteManagementContext : DbContext
{
    public WasteManagementContext(DbContextOptions<WasteManagementContext> options)
        : base(options)
    {
    }

    public DbSet<Place> Places { get; set; }
    public DbSet<GarbageBinCollectionLine> GarbageBinCollections { get; set; }
    public DbSet<AgreementConnectionLine> AgreementPlaceConnections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<GarbageBinCollectionLine>()
            .HasNoKey();
    }
}
