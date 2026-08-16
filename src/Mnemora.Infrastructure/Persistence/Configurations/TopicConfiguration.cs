using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;

namespace Mnemora.Infrastructure.Persistence.Configurations;

public sealed class TopicConfiguration
    : IEntityTypeConfiguration<Topic>
{
    public void Configure(
        EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topics");

        builder.HasKey(topic => topic.Id);

        builder.Property(topic => topic.Id)
            .HasConversion(
                id => id.Value,
                value => TopicId.Create(value).Value)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(topic => topic.SectionId)
            .HasConversion(
                id => id.Value,
                value => SectionId.Create(value).Value)
            .IsRequired()
            .HasColumnName("section_id");

        builder.Property(topic => topic.Name)
            .HasConversion(
                name => name.Value,
                value => TopicName.Create(value).Value)
            .HasMaxLength(TopicName.MAXLENGTH)
            .UseCollation(SqliteCollations.UnicodeNoCase)
            .IsRequired()
            .HasColumnName("name");

        builder.Property(topic => topic.Color)
            .HasConversion(
                color => color.ToString(),
                value => Enum.Parse<TopicColor>(
                    value,
                    ignoreCase: true))
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("color");

        builder.Property(topic => topic.Icon)
            .HasConversion(
                icon => icon.ToString(),
                value => Enum.Parse<TopicIcon>(
                    value,
                    ignoreCase: true))
            .HasMaxLength(64)
            .IsRequired()
            .HasColumnName("icon");

        builder.Property(topic => topic.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(topic => topic.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        builder.Property(topic => topic.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(Topic.DefaultDisplayOrder)
            .IsRequired();

        builder.HasIndex(topic => new { topic.SectionId, topic.Name })
            .IsUnique()
            .HasDatabaseName(
                "ux_topics_section_id_name");

        builder.HasIndex(topic => new { topic.SectionId, topic.DisplayOrder })
            .HasDatabaseName("ix_topics_section_id_display_order");

        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(topic => topic.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
