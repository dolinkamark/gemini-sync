using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ymir.GeminiSync.Domain;

namespace Ymir.GeminiSync.EntityFramework.Mappings;

public class AgreementExcemptionMapping : IEntityTypeConfiguration<AgreementExcemption>
{
    public void Configure(EntityTypeBuilder<AgreementExcemption> builder)
    {
        builder.ToTable("Agreement_Excemption");

        builder.HasOne(a => a.Type)
            .WithMany()
            .HasForeignKey(a => a.ExcemptionType)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
