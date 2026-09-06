using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;

namespace Mnemora.Infrastructure.Persistence.Configurations;

public sealed class MaterialConfiguration
    : IEntityTypeConfiguration<Material>
{
    public void Configure(
        EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("materials");

        builder.HasKey(material => material.Id);

        builder.Property(material => material.Id)
            .HasConversion(
                id => id.Value,
                value => MaterialId.Create(value).Value)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(material => material.TopicId)
            .HasConversion(
                id => id.Value,
                value => TopicId.Create(value).Value)
            .HasColumnName("topic_id")
            .IsRequired();

        builder.Property(material => material.ContainerId)
            .HasConversion(
                id => id.Value,
                value => LibraryContainerId.Create(value).Value)
            .HasColumnName("container_id")
            .IsRequired();

        builder.Property(material => material.Title)
            .HasConversion(
                title => title.Value,
                value => MaterialTitle.Create(value).Value)
            .HasColumnName("title")
            .HasMaxLength(MaterialTitle.MaxLength)
            .UseCollation(SqliteCollations.UnicodeNoCase)
            .IsRequired();

        builder.Property(material => material.Difficulty)
            .HasConversion<int>()
            .HasColumnName("difficulty")
            .IsRequired();

        builder.Property(material => material.Icon)
            .HasConversion(
                icon => icon.Key,
                value => MaterialIcon.Create(value).Value)
            .HasColumnName("icon")
            .HasMaxLength(MaterialIcon.MaxKeyLength)
            .IsRequired();

        builder.Property(material => material.LearningRevision)
            .HasColumnName("learning_revision")
            .IsRequired();

        builder.Property(material => material.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(material => material.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(material => material.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(Material.DefaultDisplayOrder)
            .HasSentinel(Material.DefaultDisplayOrder)
            .IsRequired();

        ConfigureDiscriminator(builder);
        ConfigureExperienceRewards(builder);
        ConfigureTags(builder);

        builder.HasOne<LibraryContainer>()
            .WithMany()
            .HasForeignKey(material => material.ContainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(material => material.TopicId)
            .HasDatabaseName("ix_materials_topic_id");

        builder.HasIndex(material => material.ContainerId)
            .HasDatabaseName("ix_materials_container_id");

        builder.HasIndex(nameof(Material.ContainerId), "type")
            .HasDatabaseName("ix_materials_container_id_type");

        builder.HasIndex(material => new { material.ContainerId, material.DisplayOrder, material.Id })
            .HasDatabaseName("ix_materials_container_id_display_order_id");

        builder.HasIndex(material => new { material.ContainerId, material.UpdatedAt, material.Id })
            .HasDatabaseName("ix_materials_container_id_updated_at_id");

        builder.HasIndex(material => new { material.ContainerId, material.CreatedAt, material.Id })
            .HasDatabaseName("ix_materials_container_id_created_at_id");

        builder.HasIndex(material => new { material.ContainerId, material.Title, material.Id })
            .HasDatabaseName("ix_materials_container_id_title_id");

        builder.HasIndex(nameof(Material.TopicId), "type")
            .HasDatabaseName("ix_materials_topic_id_type");

        builder.HasIndex(material => new { material.TopicId, material.DisplayOrder, material.Id })
            .HasDatabaseName("ix_materials_topic_id_display_order_id");

        builder.HasIndex(material => new { material.TopicId, material.UpdatedAt, material.Id })
            .HasDatabaseName("ix_materials_topic_id_updated_at_id");

        builder.HasIndex(material => new { material.TopicId, material.CreatedAt, material.Id })
            .HasDatabaseName("ix_materials_topic_id_created_at_id");

        builder.HasIndex(material => new { material.TopicId, material.Title, material.Id })
            .HasDatabaseName("ix_materials_topic_id_title_id");
    }

    private static void ConfigureDiscriminator(
        EntityTypeBuilder<Material> builder)
    {
        builder.Ignore(material => material.Type);

        builder.HasDiscriminator<MaterialType>("type")
            .HasValue<Article>(MaterialType.Article)
            .HasValue<Question>(MaterialType.Question);

        builder.Property<MaterialType>("type")
            .HasConversion<int>()
            .HasColumnName("type")
            .IsRequired();
    }

    private static void ConfigureExperienceRewards(
        EntityTypeBuilder<Material> builder)
    {
        builder.OwnsOne(
            material => material.ExperienceRewards,
            rewardsBuilder =>
            {
                rewardsBuilder
                    .Property(rewards => rewards.StudyPoints)
                    .HasColumnName("study_points")
                    .IsRequired();

                rewardsBuilder
                    .Property(rewards => rewards.ReviewPoints)
                    .HasColumnName("review_points")
                    .IsRequired();
            });

        builder.Navigation(
                material => material.ExperienceRewards)
            .IsRequired();
    }

    private static void ConfigureTags(
        EntityTypeBuilder<Material> builder)
    {
        builder.OwnsMany(
            material => material.Tags,
            tagsBuilder =>
            {
                tagsBuilder.ToTable("material_tags");

                tagsBuilder
                    .WithOwner()
                    .HasForeignKey("material_id");

                tagsBuilder.Property(tag => tag.Value)
                    .HasColumnName("value")
                    .HasMaxLength(MaterialTag.MaxLength)
                    .UseCollation(
                        SqliteCollations.UnicodeNoCase)
                    .IsRequired();

                tagsBuilder.HasKey(
                    "material_id",
                    nameof(MaterialTag.Value));

                tagsBuilder.HasIndex(tag => tag.Value)
                    .HasDatabaseName(
                        "ix_material_tags_value");
            });

        builder.Navigation(material => material.Tags)
            .HasField("_tags")
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}
