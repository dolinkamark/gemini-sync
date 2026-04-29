using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.EntityFramework.Mappings;

public class PlaceMapping : IEntityTypeConfiguration<Place>
{
    public void Configure(EntityTypeBuilder<Place> builder)
    {
        builder.ToTable("Place");

        builder.HasKey(p => new { p.GPSLSCustomerId, p.PASystem, p.AgreementId, p.PlaceNr });
    }
}
