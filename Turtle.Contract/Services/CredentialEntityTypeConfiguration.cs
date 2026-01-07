using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public sealed class CredentialEntityTypeConfiguration : IEntityTypeConfiguration<CredentialEntity>
{
    public void Configure(EntityTypeBuilder<CredentialEntity> builder)
    {
        builder.HasKey(e => e.Id);

        builder
            .Property(e => e.Id)
            .ValueGeneratedNever()
            .Metadata.SetValueComparer(
                new ValueComparer<Guid>((c1, c2) => c1 == c2, c => c.GetHashCode(), c => c)
            );

        builder.Property(e => e.Name).HasMaxLength(255);
        builder.Property(e => e.Login).HasMaxLength(255);
        builder.Property(e => e.Key).HasMaxLength(255);
        builder.Property(e => e.Regex).HasMaxLength(255);
        builder.Property(e => e.CustomAvailableCharacters).HasMaxLength(1000);

        builder
            .Property(e => e.ParentId)
            .Metadata.SetValueComparer(
                new ValueComparer<Guid?>((c1, c2) => c1 == c2, c => c.GetHashCode(), c => c)
            );
    }
}
