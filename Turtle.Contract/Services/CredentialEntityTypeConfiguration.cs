using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestor.Db.Helpers;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public sealed class CredentialEntityTypeConfiguration : IEntityTypeConfiguration<CredentialEntity>
{
    public void Configure(EntityTypeBuilder<CredentialEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever().SetComparerStruct();
        builder.Property(e => e.Name).HasMaxLength(255).SetComparerClass();
        builder.Property(e => e.Login).HasMaxLength(255).SetComparerClass();
        builder.Property(e => e.Key).HasMaxLength(255).SetComparerClass();
        builder.Property(e => e.Regex).HasMaxLength(255).SetComparerClass();
        builder.Property(e => e.CustomAvailableCharacters).HasMaxLength(1000).SetComparerClass();
        builder.Property(e => e.ParentId).SetComparerNullStruct();
        builder.Property(e => e.IsAvailableUpperLatin).SetComparerStruct();
        builder.Property(e => e.IsAvailableLowerLatin).SetComparerStruct();
        builder.Property(e => e.IsAvailableNumber).SetComparerStruct();
        builder.Property(e => e.IsAvailableSpecialSymbols).SetComparerStruct();
        builder.Property(e => e.Length).SetComparerStruct();
        builder.Property(e => e.Type).SetComparerStruct();
        builder.Property(e => e.OrderIndex).SetComparerStruct();
    }
}
