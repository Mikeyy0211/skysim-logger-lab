# Backend Logger Code Examples

## Example: Thin Controller

```csharp
[ApiController]
[Route("api/log-flows")]
public class LogFlowsController : ControllerBase
{
    private readonly ILogFlowService _logFlowService;

    public LogFlowsController(ILogFlowService logFlowService)
    {
        _logFlowService = logFlowService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LogFlowListDto>>> GetLogFlows(
        [FromQuery] LogFlowFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _logFlowService.GetLogFlowsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{flowId:guid}")]
    public async Task<ActionResult<LogFlowDetailDto>> GetLogFlowById(
        Guid flowId,
        CancellationToken cancellationToken)
    {
        var result = await _logFlowService.GetLogFlowByIdAsync(flowId, cancellationToken);
        if (result == null)
            return NotFound();
        return Ok(result);
    }
}
```

## Example: Service with Business Logic

```csharp
public class LogFlowService : ILogFlowService
{
    private readonly ILogger<LogFlowService> _logger;
    private readonly ILogFlowRepository _repository;
    private readonly IMapper _mapper;

    public LogFlowService(
        ILogger<LogFlowService> logger,
        ILogFlowRepository repository,
        IMapper mapper)
    {
        _logger = logger;
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<LogFlowListDto>> GetLogFlowsAsync(
        LogFlowFilterRequest request,
        CancellationToken cancellationToken)
    {
        var filter = _mapper.Map<LogFlowFilter>(request);
        var pagedResult = await _repository.GetLogFlowsAsync(filter, cancellationToken);
        return _mapper.Map<PagedResult<LogFlowListDto>>(pagedResult);
    }

    public async Task<LogFlowDetailDto?> GetLogFlowByIdAsync(
        Guid flowId,
        CancellationToken cancellationToken)
    {
        var flow = await _repository.GetFlowWithActionsAsync(flowId, cancellationToken);
        if (flow == null)
            return null;
        return _mapper.Map<LogFlowDetailDto>(flow);
    }
}
```

## Example: Kafka Consumer with Manual Offset Commit

```csharp
public class ActionLogConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActionLogConsumer> _logger;
    private readonly KafkaOptions _kafkaOptions;

    public ActionLogConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<ActionLogConsumer> logger,
        IOptions<KafkaOptions> kafkaOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false // Manual offset commit
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

        consumer.Subscribe(_kafkaOptions.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = consumer.Consume(stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IActionLogProcessor>();

                var success = await processor.ProcessAsync(consumeResult.Message.Value, stoppingToken);

                if (success)
                {
                    consumer.Commit(consumeResult); // Only commit after successful save
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing message");
            }
        }
    }
}
```

## Example: Idempotent Message Processing

```csharp
public class ActionLogProcessor : IActionLogProcessor
{
    private readonly ILogger<ActionLogProcessor> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<bool> ProcessAsync(string message, CancellationToken cancellationToken)
    {
        var actionLog = JsonSerializer.Deserialize<ActionLogMessage>(message);
        if (actionLog == null)
            return false;

        var existingAction = await _unitOfWork.LogActions
            .GetByEventIdAsync(actionLog.EventId, cancellationToken);

        if (existingAction != null)
        {
            _logger.LogInformation("Duplicate message skipped: {EventId}", actionLog.EventId);
            return true; // Already processed, return success to commit offset
        }

        using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // Process and save to database...

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to process action log: {EventId}", actionLog.EventId);
            throw;
        }
    }
}
```

## Example: Sensitive Field Masking

```csharp
public static class SensitiveFieldMasker
{
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "access_token",
        "refresh_token",
        "authorization",
        "otp",
        "cardNumber",
        "cvv",
        "paymentSecret",
        "secret",
        "token"
    };

    public static object? MaskSensitiveData(object? data)
    {
        if (data == null)
            return null;

        if (data is JsonElement jsonElement)
        {
            return MaskJsonElement(jsonElement);
        }

        var json = JsonSerializer.Serialize(data);
        var doc = JsonDocument.Parse(json);
        return MaskJsonElement(doc.RootElement);
    }

    private static object? MaskJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    if (SensitiveFields.Contains(prop.Name))
                        dict[prop.Name] = "***MASKED***";
                    else
                        dict[prop.Name] = MaskJsonElement(prop.Value);
                }
                return dict;

            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(MaskJsonElement)
                    .ToList();

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                return element.GetDecimal();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            default:
                return null;
        }
    }
}
```

## Example: Middleware Logging

```csharp
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        var requestTime = DateTime.UtcNow;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var responseTime = DateTime.UtcNow;

            var logEntry = new
            {
                CorrelationId = correlationId,
                Service = "LoggerService",
                Action = $"{context.Request.Method} {context.Request.Path}",
                RequestTime = requestTime,
                ResponseTime = responseTime,
                Duration = stopwatch.ElapsedMilliseconds,
                StatusCode = context.Response.StatusCode,
                UserId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                RequestData = MaskSensitiveData(await ReadRequestBodyAsync(context)),
                ResponseData = MaskSensitiveData(await ReadResponseBodyAsync(context))
            };

            _logger.LogInformation("HTTP Request: {LogEntry}", JsonSerializer.Serialize(logEntry));
        }
    }
}
```

## Example: Pagination DTO

```csharp
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
}

public class LogFlowFilterRequest
{
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? UserId { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
    public string? FlowType { get; set; }
    public string? CheckoutType { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
}
```
