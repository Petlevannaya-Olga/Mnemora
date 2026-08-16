using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class MaterialExperienceRewards : ValueObject
{
    public const int MinPoints = 5;
    public const int MaxPoints = 100;

    public int StudyPoints { get; }

    public int ReviewPoints { get; }

    private MaterialExperienceRewards(int studyPoints, int reviewPoints)
    {
        StudyPoints = studyPoints;
        ReviewPoints = reviewPoints;
    }

    public static Result<MaterialExperienceRewards, Error> Create(int studyPoints, int reviewPoints)
    {
        if (studyPoints is < MinPoints or > MaxPoints)
        {
            return MaterialErrors.ExperienceRewardIsOutOfRange(nameof(studyPoints), MinPoints, MaxPoints);
        }

        if (reviewPoints is < MinPoints or > MaxPoints)
        {
            return MaterialErrors.ExperienceRewardIsOutOfRange(nameof(reviewPoints), MinPoints, MaxPoints);
        }

        if (reviewPoints >= studyPoints)
        {
            return MaterialErrors.ReviewRewardMustBeLessThanStudyReward(nameof(reviewPoints));
        }

        return new MaterialExperienceRewards(studyPoints, reviewPoints);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return StudyPoints;
        yield return ReviewPoints;
    }
}