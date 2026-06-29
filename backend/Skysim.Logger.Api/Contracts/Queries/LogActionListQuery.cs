namespace Skysim.Logger.Api.Contracts.Queries;

public class LogActionListQuery
{
    public string FlowId { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
