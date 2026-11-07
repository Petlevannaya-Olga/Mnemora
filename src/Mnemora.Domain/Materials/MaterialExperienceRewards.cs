using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class MaterialExperienceRewards : ValueObject
{
    #pragma warning disable CA1707
    public const int MIN_POINTS = 5;
    #pragma warning restore CA1707
    #pragma warning disable CA1707
    public const int MAX_POINTS = 100;
    #pragma warning restore CA1707

    public int StudyPoints { get; }

    public int ReviewPoints { get; }

    private MaterialExperienceRewards(int studyPoints, int reviewPoints)
    {
        StudyPoints = studyPoints;
        ReviewPoints = reviewPoints;
    }

    public static Result<MaterialExperienceRewards, Error> Create(int studyPoints, int reviewPoints)
    {
        if (studyPoints is < MIN_POINTS or > MAX_POINTS)
        {
            return MaterialErrors.ExperienceRewardIsOutOfRange(nameof(studyPoints), MIN_POINTS, MAX_POINTS);
        }

        if (reviewPoints is < MIN_POINTS or > MAX_POINTS)
        {
            return MaterialErrors.ExperienceRewardIsOutOfRange(nameof(reviewPoints), MIN_POINTS, MAX_POINTS);
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