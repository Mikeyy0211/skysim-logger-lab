namespace Skysim.Logger.Api.Contracts.DTOs;

public record LogFlowDetailDto(LogFlowSummaryDto Flow, List<LogActionDto> Timeline);
