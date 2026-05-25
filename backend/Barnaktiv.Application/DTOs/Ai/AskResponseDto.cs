namespace Barnaktiv.Application.DTOs.Ai;

public sealed record AskResponseDto(
    string Answer,
    IReadOnlyList<ActivityAiSourceDto> Sources);

public sealed record ActivityAiSourceDto(
    Guid Id,
    string Title,
    string? SignupUrl,
    string? City,
    DateTime Date);
