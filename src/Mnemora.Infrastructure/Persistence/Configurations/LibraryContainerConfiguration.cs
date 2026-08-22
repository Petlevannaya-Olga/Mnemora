using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;

namespace Mnemora.Infrastructure.Persistence.Configurations;

public sealed class LibraryContainerConfiguration
    : IEntityTypeConfiguration<LibraryContainer>
{
    public void Configure(
        EntityTypeBuilder<LibraryContainer> builder)
    {
        builder.ToTable(
            "library_containers",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_library_containers_shape",
                    "(parent_id IS NULL AND depth = 0 " +
                    "AND name IS NULL AND color IS NULL AND icon IS NULL) " +
                    "OR (parent_id IS NOT NULL AND depth BETWEEN 1 AND 3 " +
                    "AND name IS NOT NULL AND color IS NOT NULL AND icon IS NOT NULL)");

                tableBuilder.HasCheckConstraint(
                    "ck_library_containers_display_order",
                    "display_order >= 0");
            });

        builder.HasKey(container => container.Id);

        builder.Property(container => container.Id)
            .HasConversion(
                id => id.Value,
                value => LibraryContainerId.Create(value).Value)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(container => container.SectionId)
            .HasConversion(
                id => id.Value,
                value => SectionId.Create(value).Value)
            .IsRequired()
            .HasColumnName("section_id");

        builder.Property(container => container.ParentId)
            .HasConversion(
                id => id == null
                    ? (Guid?)null
                    : id.Value,
                value => value.HasValue
                    ? LibraryContainerId.Create(value.Value).Value
                    : null)
            .HasColumnName("parent_id");

        builder.Property(container => container.Depth)
            .IsRequired()
            .HasColumnName("depth");

        builder.Property(container => container.Name)
            .HasConversion(
                name => name == null
                    ? null
                    : name.Value,
                value => value == null
                    ? null
                    : FolderName.Create(value).Value)
            .HasMaxLength(FolderName.MAXLENGTH)
            .UseCollation(SqliteCollations.UnicodeNoCase)
            .HasColumnName("name");

        builder.Property(container => container.Color)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnName("color");

        builder.Property(container => container.Icon)
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasColumnName("icon");

        builder.Property(container => container.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(container => container.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        builder.Property(container => container.DisplayOrder)
            .HasDefaultValue(LibraryContainer.DefaultDisplayOrder)
            .IsRequired()
            .HasColumnName("display_order");

        // Нужен для составного FK ниже: ребёнок обязан ссылаться
        // на родителя из того же раздела.
        builder.HasAlternateKey(
                container => new
                {
                    container.Id,
                    container.SectionId,
                })
            .HasName("ak_library_containers_id_section_id");

        builder.HasIndex(
                container => container.SectionId,
                "ux_library_containers_section_id_root")
            .IsUnique()
            .HasFilter("parent_id IS NULL");

        builder.HasIndex(
                container => new
                {
                    container.SectionId,
                    container.Depth,
                    container.DisplayOrder,
                })
            .HasDatabaseName(
                "ix_library_containers_section_id_depth_display_order");

        builder.HasIndex(
                container => new
                {
                    container.ParentId,
                    container.DisplayOrder,
                })
            .HasDatabaseName(
                "ix_library_containers_parent_id_display_order");

        builder.HasIndex(
                container => new
                {
                    container.ParentId,
                    container.Name,
                })
            .IsUnique()
            .HasFilter("parent_id IS NOT NULL")
            .HasDatabaseName(
                "ux_library_containers_parent_id_name");

        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(container => container.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<LibraryContainer>()
            .WithMany()
            .HasForeignKey(
                container => new
                {
                    container.ParentId,
                    container.SectionId,
                })
            .HasPrincipalKey(
                container => new
                {
                    container.Id,
                    container.SectionId,
                })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
