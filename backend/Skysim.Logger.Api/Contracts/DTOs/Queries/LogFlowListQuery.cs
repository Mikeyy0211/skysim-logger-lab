namespace Skysim.Logger.Api.Contracts.DTOs.Queries;

public class LogFlowListQuery
{
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? UserId { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
    public string? FlowType { get; set; }
    public string? CheckoutType { get; set; }
    public string? Status { get; set; }
    public string? ServiceName { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
