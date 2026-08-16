using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Questions.Delete;

public sealed record DeleteQuestionCommand(Guid QuestionId) : ICommandValidation;