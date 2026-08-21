using Mnemora.Application.Library.Order;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class SaveLibraryOrderCommandValidatorTests
{
    [Fact]
    public void SectionsOrder_RequiresNullParentAndUniqueNonEmptyIds()
    {
        Guid duplicate = Guid.NewGuid();
        var command = new SaveLibraryOrderCommand(
            LibraryOrderTarget.Sections,
            ParentId: Guid.NewGuid(),
            OrderedIds: [duplicate, duplicate, Guid.Empty]);

        var result = new SaveLibraryOrderCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.ParentId));
        Assert.Contains(result.Errors, error => error.PropertyName.StartsWith(nameof(command.OrderedIds)));
    }

    [Theory]
    [InlineData(LibraryOrderTarget.Topics)]
    [InlineData(LibraryOrderTarget.Materials)]
    public void ChildOrder_RequiresNonEmptyParent(LibraryOrderTarget target)
    {
        var command = new SaveLibraryOrderCommand(
            target,
            ParentId: Guid.Empty,
            OrderedIds: [Guid.NewGuid()]);

        var result = new SaveLibraryOrderCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.ParentId));
    }
}
