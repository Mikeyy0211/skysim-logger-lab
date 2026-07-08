using System.Net.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Skysim.Logger.Client.Http;
using Skysim.Logger.Client.Middlewares;
using Xunit;
using HeaderNames = Skysim.Logger.Contracts.Constants.HeaderNames;

namespace Skysim.Logger.Api.Tests.Client.Http;

public class FlowContextForwardingHandlerTests
{
    #region Forwards headers when present

    [Fact]
    public async Task SendAsync_HttpContextHasXFlowId_ForwardsToOutboundRequest()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.FlowId] = "downstream-flow-123";

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == HeaderNames.FlowId);
        capturedRequest.Headers.GetValues(HeaderNames.FlowId).Should().Contain("downstream-flow-123");
    }

    [Fact]
    public async Task SendAsync_HttpContextHasXCorrelationId_ForwardsToOutboundRequest()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.CorrelationId] = "downstream-corr-456";

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == HeaderNames.CorrelationId);
        capturedRequest.Headers.GetValues(HeaderNames.CorrelationId).Should().Contain("downstream-corr-456");
    }

    [Fact]
    public async Task SendAsync_HttpContextHasBothHeaders_ForwardsBothToOutboundRequest()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.FlowId] = "flow-abc";
        context.Request.Headers[HeaderNames.CorrelationId] = "corr-xyz";

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == HeaderNames.FlowId);
        capturedRequest.Headers.Should().Contain(h => h.Key == HeaderNames.CorrelationId);
        capturedRequest.Headers.GetValues(HeaderNames.FlowId).Should().Contain("flow-abc");
        capturedRequest.Headers.GetValues(HeaderNames.CorrelationId).Should().Contain("corr-xyz");
    }

    #endregion

    #region Does not overwrite existing outbound headers

    [Fact]
    public async Task SendAsync_OutboundRequestAlreadyHasXFlowId_DoesNotOverwrite()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.FlowId] = "incoming-flow-id";

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");
        request.Headers.TryAddWithoutValidation(HeaderNames.FlowId, "pre-existing-flow-id");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues(HeaderNames.FlowId).Should().Contain("pre-existing-flow-id");
        capturedRequest.Headers.GetValues(HeaderNames.FlowId).Should().NotContain("incoming-flow-id");
    }

    [Fact]
    public async Task SendAsync_OutboundRequestAlreadyHasXCorrelationId_DoesNotOverwrite()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.CorrelationId] = "incoming-corr-id";

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");
        request.Headers.TryAddWithoutValidation(HeaderNames.CorrelationId, "pre-existing-corr-id");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues(HeaderNames.CorrelationId).Should().Contain("pre-existing-corr-id");
        capturedRequest.Headers.GetValues(HeaderNames.CorrelationId).Should().NotContain("incoming-corr-id");
    }

    #endregion

    #region Handles missing/null HttpContext gracefully

    [Fact]
    public async Task SendAsync_HttpContextIsNull_DoesNotThrow()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var handler = new FlowContextForwardingHandler(accessorMock.Object) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        Func<Task> act = () => client.SendAsync(request);

        // Assert
        await act.Should().NotThrowAsync();
        capturedRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_HttpContextHasNoFlowHeaders_DoesNotAddAnyHeaders()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        // No X-Flow-Id or X-Correlation-Id headers and no FlowContext item

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Any(h => h.Key == HeaderNames.FlowId).Should().BeFalse();
        capturedRequest.Headers.Any(h => h.Key == HeaderNames.CorrelationId).Should().BeFalse();
    }

    #endregion

    #region Fallback to HttpContext.Items["FlowContext"] (set by LoggerMiddleware even when inbound request had no X-Flow-Id)

    [Fact]
    public async Task SendAsync_FlowContextInHttpContextItems_WithoutInboundHeaders_ForwardsFlowId()
    {
        // Arrange: simulate LoggerMiddleware having synthesised a flowId for a request
        // that arrived with no X-Flow-Id / X-Correlation-Id header. The middleware
        // stores the resolved FlowContext under "FlowContext" in HttpContext.Items.
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Items[FlowContext.HttpContextItemKey] = new FlowContext
        {
            FlowId = "generated-flow-XYZ",
            CorrelationId = "generated-corr-XYZ"
        };

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues(HeaderNames.FlowId).Should().Contain("generated-flow-XYZ");
        capturedRequest.Headers.GetValues(HeaderNames.CorrelationId).Should().Contain("generated-corr-XYZ");
    }

    [Fact]
    public async Task SendAsync_FlowContextItemPreferredOverInboundHeaders_WhenBothPresent()
    {
        // Arrange: FlowContext is the middleware's authoritative source. If both
        // an inbound header and a typed FlowContext are present, the typed FlowContext wins.
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.FlowId] = "inbound-flow";
        context.Request.Headers[HeaderNames.CorrelationId] = "inbound-corr";
        context.Items[FlowContext.HttpContextItemKey] = new FlowContext
        {
            FlowId = "items-flow",
            CorrelationId = "items-corr"
        };

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues(HeaderNames.FlowId).Should().Contain("items-flow");
        capturedRequest.Headers.GetValues(HeaderNames.CorrelationId).Should().Contain("items-corr");
    }

    [Fact]
    public async Task SendAsync_FlowContextItemPresent_DoesNotOverwriteExistingOutboundHeader()
    {
        // Arrange: outbound already carries X-Flow-Id — caller wins, regardless of source.
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Items[FlowContext.HttpContextItemKey] = new FlowContext
        {
            FlowId = "items-flow",
            CorrelationId = "items-corr"
        };

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");
        request.Headers.TryAddWithoutValidation(HeaderNames.FlowId, "explicit-outbound-flow");
        request.Headers.TryAddWithoutValidation(HeaderNames.CorrelationId, "explicit-outbound-corr");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues(HeaderNames.FlowId).Should().Contain("explicit-outbound-flow");
        capturedRequest.Headers.GetValues(HeaderNames.FlowId).Should().NotContain("items-flow");
        capturedRequest.Headers.GetValues(HeaderNames.CorrelationId).Should().Contain("explicit-outbound-corr");
        capturedRequest.Headers.GetValues(HeaderNames.CorrelationId).Should().NotContain("items-corr");
    }

    [Fact]
    public async Task SendAsync_FlowContextItemMissing_FallsBackToInboundHeaders()
    {
        // Arrange: legacy path — service did not register LoggerMiddleware but
        // did have X-Flow-Id on the inbound request. Handler should still forward.
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.FlowId] = "legacy-inbound-flow";
        context.Request.Headers[HeaderNames.CorrelationId] = "legacy-inbound-corr";
        // No FlowContext item

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        await client.SendAsync(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues(HeaderNames.FlowId).Should().Contain("legacy-inbound-flow");
        capturedRequest.Headers.GetValues(HeaderNames.CorrelationId).Should().Contain("legacy-inbound-corr");
    }

    [Fact]
    public async Task SendAsync_FlowContextItemIsWrongType_IgnoresItAndDoesNotForward()
    {
        // Arrange: defensive — if some other middleware stored a non-FlowContext
        // value under the key, the handler should not crash and not forward anything.
        HttpRequestMessage? capturedRequest = null;
        var innerHandler = new CapturingTestHandler(req => capturedRequest = req);

        var context = new DefaultHttpContext();
        context.Items[FlowContext.HttpContextItemKey] = "not-a-flow-context";

        var accessor = CreateAccessor(context);
        var handler = new FlowContextForwardingHandler(accessor) { InnerHandler = innerHandler };
        var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test");

        // Act
        Func<Task> act = () => client.SendAsync(request);

        // Assert
        await act.Should().NotThrowAsync();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Any(h => h.Key == HeaderNames.FlowId).Should().BeFalse();
        capturedRequest.Headers.Any(h => h.Key == HeaderNames.CorrelationId).Should().BeFalse();
    }

    #endregion

    #region Constructor validation

    [Fact]
    public void Constructor_NullHttpContextAccessor_ThrowsArgumentNullException()
    {
        var act = () => new FlowContextForwardingHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpContextAccessor");
    }

    #endregion

    #region Helper types and methods

    private static IHttpContextAccessor CreateAccessor(HttpContext context)
    {
        var mock = new Mock<IHttpContextAccessor>();
        mock.Setup(a => a.HttpContext).Returns(context);
        return mock.Object;
    }

    /// <summary>
    /// Test handler that captures the outbound request and passes it to a callback,
    /// then returns a successful response.
    /// </summary>
    private sealed class CapturingTestHandler : HttpMessageHandler
    {
        private readonly Action<HttpRequestMessage> _onCapture;

        public CapturingTestHandler(Action<HttpRequestMessage> onCapture)
        {
            _onCapture = onCapture;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _onCapture(request);
            return Task.FromResult(new HttpResponseMessage());
        }
    }

    #endregion
}
