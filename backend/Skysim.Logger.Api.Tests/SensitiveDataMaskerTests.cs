using FluentAssertions;
using Skysim.Logger.Api.Common;
using Xunit;

namespace Skysim.Logger.Api.Tests;

public class SensitiveDataMaskerTests
{
    private readonly SensitiveDataMasker _masker = new();

    [Fact]
    public void MaskJson_TopLevelSensitiveField_ShouldBeMasked()
    {
        var json = """{"password":"supersecret","username":"john"}""";

        var result = _masker.MaskJson(json);

        result.Should().Contain("\"password\":\"***\"");
        result.Should().Contain("\"username\":\"john\"");
        result.Should().NotContain("supersecret");
    }

    [Fact]
    public void MaskJson_NestedSensitiveField_ShouldBeMasked()
    {
        var json = """
            {
                "data": {
                    "authorization": "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9",
                    "userId": "user-123"
                }
            }
            """;

        var result = _masker.MaskJson(json);

        result.Should().Contain("\"authorization\":\"***\"");
        result.Should().Contain("\"userId\":\"user-123\"");
        result.Should().NotContain("Bearer");
    }

    [Fact]
    public void MaskJson_ArrayWithSensitiveField_ShouldMaskAll()
    {
        var json = """
            {
                "tokens": [
                    {"token": "abc123"},
                    {"token": "def456"}
                ]
            }
            """;

        var result = _masker.MaskJson(json);

        result.Should().Contain("\"token\":\"***\"");
        result.Should().NotContain("abc123");
        result.Should().NotContain("def456");
    }

    [Fact]
    public void MaskJson_NonSensitiveFields_ShouldRemainUnchanged()
    {
        var json = """
            {
                "orderId": "ORD-001",
                "status": "SUCCESS",
                "amount": 99.99
            }
            """;

        var result = _masker.MaskJson(json);

        result.Should().Contain("\"orderId\":\"ORD-001\"");
        result.Should().Contain("\"status\":\"SUCCESS\"");
        result.Should().Contain("99.99");
    }

    [Fact]
    public void MaskJson_NullOrEmpty_ShouldReturnUnchanged()
    {
        _masker.MaskJson(null!).Should().BeNull();
        _masker.MaskJson("").Should().BeEmpty();
    }

    [Fact]
    public void MaskJson_AllSensitiveFields_ShouldBeMasked()
    {
        var json = """
            {
                "password":"secret1",
                "access_token":"token1",
                "refresh_token":"token2",
                "authorization":"auth1",
                "otp":"123456",
                "cardNumber":"4111111111111111",
                "cvv":"123",
                "paymentSecret":"paysec",
                "secret":"mysecret",
                "token":"bearer-token",
                "orderId":"ORD-001"
            }
            """;

        var result = _masker.MaskJson(json);

        result.Should().Contain("\"password\":\"***\"");
        result.Should().Contain("\"access_token\":\"***\"");
        result.Should().Contain("\"refresh_token\":\"***\"");
        result.Should().Contain("\"authorization\":\"***\"");
        result.Should().Contain("\"otp\":\"***\"");
        result.Should().Contain("\"cardNumber\":\"***\"");
        result.Should().Contain("\"cvv\":\"***\"");
        result.Should().Contain("\"paymentSecret\":\"***\"");
        result.Should().Contain("\"secret\":\"***\"");
        result.Should().Contain("\"token\":\"***\"");
        result.Should().Contain("\"orderId\":\"ORD-001\"");
    }

    [Fact]
    public void MaskJson_CaseInsensitiveKeys_ShouldBeMasked()
    {
        var json = """
            {
                "PASSWORD":"secret1",
                "Access_Token":"token1",
                "CARDNUMBER":"4111111111111111"
            }
            """;

        var result = _masker.MaskJson(json);

        result.Should().Contain("\"PASSWORD\":\"***\"");
        result.Should().Contain("\"Access_Token\":\"***\"");
        result.Should().Contain("\"CARDNUMBER\":\"***\"");
    }

    [Fact]
    public void SensitiveFields_DenyList_ShouldContainAllRequiredFields()
    {
        SensitiveFields.DenyList.Should().HaveCount(10);
        SensitiveFields.DenyList.Should().Contain("password");
        SensitiveFields.DenyList.Should().Contain("access_token");
        SensitiveFields.DenyList.Should().Contain("refresh_token");
        SensitiveFields.DenyList.Should().Contain("authorization");
        SensitiveFields.DenyList.Should().Contain("otp");
        SensitiveFields.DenyList.Should().Contain("cardNumber");
        SensitiveFields.DenyList.Should().Contain("cvv");
        SensitiveFields.DenyList.Should().Contain("paymentSecret");
        SensitiveFields.DenyList.Should().Contain("secret");
        SensitiveFields.DenyList.Should().Contain("token");
    }

    [Fact]
    public void SensitiveFields_IsSensitive_ShouldBeCaseInsensitive()
    {
        SensitiveFields.IsSensitive("PASSWORD").Should().BeTrue();
        SensitiveFields.IsSensitive("password").Should().BeTrue();
        SensitiveFields.IsSensitive("Access_Token").Should().BeTrue();
        SensitiveFields.IsSensitive("CARDNUMBER").Should().BeTrue();
        SensitiveFields.IsSensitive("orderId").Should().BeFalse();
    }
}
