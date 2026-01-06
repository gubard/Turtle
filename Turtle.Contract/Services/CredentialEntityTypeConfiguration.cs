using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public sealed class CredentialEntityTypeConfiguration : IEntityTypeConfiguration<CredentialEntity>
{
    public void Configure(EntityTypeBuilder<CredentialEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).HasMaxLength(255);
        builder.Property(e => e.Login).HasMaxLength(255);
        builder.Property(e => e.Key).HasMaxLength(255);
        builder.Property(e => e.Regex).HasMaxLength(255);
        builder.Property(e => e.CustomAvailableCharacters).HasMaxLength(1000);
    }
}
