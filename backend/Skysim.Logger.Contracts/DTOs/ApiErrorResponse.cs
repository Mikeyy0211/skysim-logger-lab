namespace Skysim.Logger.Contracts.DTOs;

public record ApiErrorResponse(ApiErrorDetail Error);
public record ApiErrorDetail(string Code, string Message, List<ApiFieldError>? Details);
public record ApiFieldError(string Field, string Message);
