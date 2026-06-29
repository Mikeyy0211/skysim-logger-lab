using FluentAssertions;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Api.Validators;
using Xunit;

namespace Skysim.Logger.Api.Tests.Validators;

public class LogActionListQueryValidatorTests
{
    private readonly LogActionListQueryValidator _validator = new();

    [Fact]
    public void ValidQuery_Passes()
    {
        var query = new LogActionListQuery { FlowId = "flow-123", Page = 1, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyFlowId_Fails()
    {
        var query = new LogActionListQuery { FlowId = "", Page = 1, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FlowId");
    }

    [Fact]
    public void WhitespaceFlowId_Fails()
    {
        var query = new LogActionListQuery { FlowId = "   ", Page = 1, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Page_Zero_Fails()
    {
        var query = new LogActionListQuery { FlowId = "flow-123", Page = 0, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Page");
    }

    [Fact]
    public void Page_Negative_Fails()
    {
        var query = new LogActionListQuery { FlowId = "flow-123", Page = -1, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void PageSize_200_Fails()
    {
        var query = new LogActionListQuery { FlowId = "flow-123", Page = 1, PageSize = 200 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSize_100_Passes()
    {
        var query = new LogActionListQuery { FlowId = "flow-123", Page = 1, PageSize = 100 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OptionalServiceName_Valid()
    {
        var query = new LogActionListQuery { FlowId = "flow-123", ServiceName = "Payment", Page = 1, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OptionalServiceName_Missing_IsValid()
    {
        var query = new LogActionListQuery { FlowId = "flow-123", Page = 1, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
