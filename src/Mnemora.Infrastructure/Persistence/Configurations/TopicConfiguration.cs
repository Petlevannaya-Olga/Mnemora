using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;

namespace Mnemora.Infrastructure.Persistence.Configurations;

public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topics");

        builder.HasKey(topic => topic.Id);

        builder.Property(topic => topic.Id)
            .HasConversion(
                id => id.Value,
                value => new TopicId(value))
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(topic => topic.SectionId)
            .HasConversion(
                id => id.Value,
                value => new SectionId(value))
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

        builder.Property(topic => topic.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(topic => topic.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        builder.HasIndex(topic => new
            {
                topic.SectionId,
                topic.Name
            })
            .IsUnique()
            .HasDatabaseName("ux_topics_section_id_name");

        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(topic => topic.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}