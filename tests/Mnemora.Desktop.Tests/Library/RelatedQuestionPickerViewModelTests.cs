using FluentAssertions;
using Mnemora.Contracts;
using Mnemora.Desktop.ViewModels.Library;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class RelatedQuestionPickerViewModelTests
{
    [Fact]
    public void SelectContainer_FiltersQuestionsByContainerIdNotDisplayName()
    {
        Guid sectionId = Guid.NewGuid();
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        var options = new[]
        {
            CreateOption(Guid.NewGuid(), firstId, "Общие", sectionId),
            CreateOption(Guid.NewGuid(), secondId, "Общие", sectionId),
        };

        var viewModel = new RelatedQuestionPickerViewModel(
            options, Array.Empty<Guid>(), firstId, "Общие");

        viewModel.Sections.Should().ContainSingle();
        viewModel.Sections[0].Containers.Should().HaveCount(2);

        viewModel.SelectContainer(secondId);

        viewModel.VisibleQuestions.Should().ContainSingle();
        viewModel.VisibleQuestions[0].Id.Should().Be(options[1].Id);
    }

    [Fact]
    public void SelectedQuestionFromOtherContainer_IsMarkedForMove()
    {
        Guid targetContainerId = Guid.NewGuid();
        Guid otherContainerId = Guid.NewGuid();
        Guid otherQuestionId = Guid.NewGuid();
        Guid sectionId = Guid.NewGuid();
        var options = new[]
        {
            CreateOption(Guid.NewGuid(), targetContainerId, "Текущая", sectionId),
            CreateOption(otherQuestionId, otherContainerId, "Соседняя", sectionId),
        };

        var viewModel = new RelatedQuestionPickerViewModel(
            options, new[] { otherQuestionId }, targetContainerId, "Текущая");

        viewModel.CrossContainerSelectedCount.Should().Be(1);
        viewModel.HasCrossContainerSelection.Should().BeTrue();
        viewModel.GetSelectedOptions().Should().ContainSingle()
            .Which.ContainerId.Should().Be(otherContainerId);
    }

    [Fact]
    public void QuestionsInRootContainer_AreVisibleUnderRootLocation()
    {
        Guid rootContainerId = Guid.NewGuid();
        var options = new[]
        {
            CreateOption(Guid.NewGuid(), rootContainerId, "Без папки", Guid.NewGuid()),
        };

        var viewModel = new RelatedQuestionPickerViewModel(
            options, Array.Empty<Guid>(), rootContainerId, "Без папки");

        viewModel.VisibleQuestions.Should().ContainSingle();
        viewModel.VisibleQuestions[0].PathText.Should().Contain("Без папки");
    }

    private static StandaloneQuestionPickerOptionDto CreateOption(
        Guid id, Guid containerId, string containerName, Guid sectionId) =>
        new(
            id,
            "Вопрос",
            "Easy",
            20,
            10,
            containerId,
            containerName,
            sectionId,
            "Раздел");
}
