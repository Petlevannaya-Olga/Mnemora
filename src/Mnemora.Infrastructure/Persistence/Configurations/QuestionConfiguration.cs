using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemora.Domain.Materials;

namespace Mnemora.Infrastructure.Persistence.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(question => question.ArticleId)
            .HasConversion(
                id => id!.Value,
                value => MaterialId.Create(value).Value)
            .HasColumnName("article_id")
            .IsRequired(false);

        builder.HasOne<Article>()
            .WithMany()
            .HasForeignKey(question => question.ArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(question => question.ArticleId);
    }
}