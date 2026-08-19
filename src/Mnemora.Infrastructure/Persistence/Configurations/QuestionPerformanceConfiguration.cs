using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemora.Domain.Materials;

namespace Mnemora.Infrastructure.Persistence.Configurations;

/// <summary>
/// Read-side indexes for library paging and article question counters.
/// Kept separate from the relationship mapping so the performance contract is explicit.
/// </summary>
public sealed class QuestionPerformanceConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasIndex(question => question.ArticleId)
            .HasDatabaseName("ix_materials_article_id");

        builder.HasIndex(question => new { question.TopicId, question.ArticleId })
            .HasDatabaseName("ix_materials_topic_id_article_id");
    }
}
