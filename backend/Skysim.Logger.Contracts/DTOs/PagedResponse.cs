namespace Skysim.Logger.Contracts.DTOs;

public record PagedResponse<T>(
    List<T> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);
