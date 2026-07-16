namespace Skysim.Logger.Api.Contracts.Queries;

/// <summary>
/// Query parameters for listing business flows grouped by order code.
/// </summary>
public class BusinessFlowListQuery
{
    /// <summary>
    /// Keyword search across orderCode, paymentId, transactionId,
    /// userEmail, customerEmail, customerPhone, lastMessage, and serviceName.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Filters by the computed business status of an order group.
    /// </summary>
    public string? Status { get; set; }

    public string? FromDate { get; set; }
    public string? ToDate { get; set; }

    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
