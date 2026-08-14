using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemora.Domain.Sections;

namespace Mnemora.Infrastructure.Persistence.Configurations;

public sealed class SectionConfiguration
    : IEntityTypeConfiguration<Section>
{
    public void Configure(
        EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("sections");

        builder.HasKey(section => section.Id);

        builder.Property(section =>
                section.Id)
            .HasConversion(
                id => id.Value,
                value => new SectionId(value))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(section => section.Name)
            .HasConversion(
                name => name.Value,
                value => SectionName.Create(value).Value)
            .HasMaxLength(SectionName.MAXLENGTH)
            .UseCollation(SqliteCollations.UnicodeNoCase)
            .IsRequired()
            .HasColumnName("name");

        builder.Property(section => section.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(section => section.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(section => section.Name)
            .IsUnique()
            .HasDatabaseName(
                "ux_sections_name");
    }
}