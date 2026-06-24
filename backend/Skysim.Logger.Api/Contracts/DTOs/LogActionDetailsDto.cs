namespace Skysim.Logger.Api.Contracts.DTOs;

public record LogActionDetailsDto(
    LogActionDto Action,
    string? RequestPayload,
    string? ResponsePayload,
    string? ErrorPayload,
    string? Metadata);
