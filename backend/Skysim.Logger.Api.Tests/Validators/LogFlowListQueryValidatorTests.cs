using FluentAssertions;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Api.Validators;
using Xunit;

namespace Skysim.Logger.Api.Tests.Validators;

public class LogFlowListQueryValidatorTests
{
    private readonly LogFlowListQueryValidator _validator = new();

    [Fact]
    public void ValidQuery_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Page_Zero_Fails()
    {
        var query = new LogFlowListQuery { Page = 0, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Page");
    }

    [Fact]
    public void Page_Negative_Fails()
    {
        var query = new LogFlowListQuery { Page = -1, PageSize = 20 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void PageSize_Zero_Fails()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 0 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSize_101_Fails()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 101 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSize_100_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 100 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidSortBy_Fails()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, SortBy = "unknownField" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SortBy");
    }

    [Fact]
    public void ValidSortBy_CreatedAt_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, SortBy = "createdAt" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidSortBy_Status_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, SortBy = "status" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidSortDirection_Fails()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, SortDirection = "invalid" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SortDirection");
    }

    [Fact]
    public void SortDirection_Asc_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, SortDirection = "asc" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SortDirection_Desc_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, SortDirection = "desc" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NullSortBy_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, SortBy = null };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NullSortDirection_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, SortDirection = null };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidFromDate_Fails()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, FromDate = "not-a-date" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FromDate");
    }

    [Fact]
    public void ValidFromDate_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, FromDate = "2026-06-01" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidToDate_Fails()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, ToDate = "invalid" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ToDate");
    }

    [Fact]
    public void ValidToDate_Passes()
    {
        var query = new LogFlowListQuery { Page = 1, PageSize = 20, ToDate = "2026-06-22" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidWithAllOptionalFields_Passes()
    {
        var query = new LogFlowListQuery
        {
            CustomerEmail = "alice@example.com",
            CustomerPhone = "+1234567890",
            UserId = "user-123",
            OrderId = "ORD-456",
            PaymentId = "PAY-789",
            FlowType = "CheckoutEsim",
            CheckoutType = "Guest",
            Status = "InProgress",
            ServiceName = "Payment",
            FromDate = "2026-06-01",
            ToDate = "2026-06-22",
            Page = 1,
            PageSize = 20,
            SortBy = "createdAt",
            SortDirection = "desc"
        };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
