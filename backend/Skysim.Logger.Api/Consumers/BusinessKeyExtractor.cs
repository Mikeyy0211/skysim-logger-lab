using System.Text.Json;

namespace Skysim.Logger.Api.Consumers;

/// <summary>
/// Extracts business keys (orderCode, paymentId, transactionId) from raw request/response
/// payloads produced by upstream services. Handles two real-world shapes:
///   - Form A: a JSON object containing nested keys (e.g. data.payment.billOrder).
///   - Form B: a wrapper object whose "body" field is a JSON string that must be parsed first.
/// Never throws on non-JSON input.
/// </summary>
public static class BusinessKeyExtractor
{
    private static readonly string[] OrderCodeKeys =
    [
        "orderCode",
        "order_code",
        "orderNo",
        "orderNumber",
        "billOrder",
        "bill_order"
    ];

    private static readonly string[] PaymentIdKeys =
    [
        "paymentId",
        "payment_id",
        "transPaymentId",
        "trans_payment_id"
    ];

    private static readonly string[] TransactionIdKeys =
    [
        "transactionId",
        "transaction_id",
        "transId",
        "trans_id"
    ];

    private static readonly string[] CustomerEmailKeys =
    [
        "customerEmail",
        "customer_email",
        "buyerEmail",
        "receiverEmail",
        "email"
    ];

    private static readonly string[] CustomerPhoneKeys =
    [
        "customerPhone",
        "customer_phone",
        "phoneNumber",
        "phone_number",
        "phone",
        "mobile",
        "mobilePhone"
    ];

    private static readonly string[] CustomerNameKeys =
    [
        "customerName",
        "customer_name",
        "fullname",
        "fullName",
        "name"
    ];

    /// <summary>
    /// Tries to extract an order code from the payload. Returns null when no match is found.
    /// </summary>
    public static string? ExtractOrderCode(string? rawJson) =>
        ExtractFirstMatch(rawJson, OrderCodeKeys);

    /// <summary>
    /// Tries to extract a payment id from the payload. Returns null when no match is found.
    /// </summary>
    public static string? ExtractPaymentId(string? rawJson) =>
        ExtractFirstMatch(rawJson, PaymentIdKeys);

    /// <summary>
    /// Tries to extract a transaction id from the payload. Returns null when no match is found.
    /// </summary>
    public static string? ExtractTransactionId(string? rawJson) =>
        ExtractFirstMatch(rawJson, TransactionIdKeys);

    /// <summary>
    /// Tries to extract a customer email from the payload. Returns null when no match is found.
    /// </summary>
    public static string? ExtractCustomerEmail(string? rawJson) =>
        ExtractFirstMatch(rawJson, CustomerEmailKeys);

    /// <summary>
    /// Tries to extract a customer phone from the payload. Returns null when no match is found.
    /// </summary>
    public static string? ExtractCustomerPhone(string? rawJson) =>
        ExtractFirstMatch(rawJson, CustomerPhoneKeys);

    /// <summary>
    /// Tries to extract a customer name from the payload. Returns null when no match is found.
    /// </summary>
    public static string? ExtractCustomerName(string? rawJson) =>
        ExtractFirstMatch(rawJson, CustomerNameKeys);

    /// <summary>
    /// Returns the first non-empty string value found for any of the candidate property names,
    /// searching recursively through the JSON tree.
    /// </summary>
    private static string? ExtractFirstMatch(string? rawJson, IReadOnlyList<string> candidateKeys)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        JsonElement root;
        try
        {
            // Clone the root so the element survives disposal of the parent JsonDocument.
            using var doc = JsonDocument.Parse(rawJson);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }

        return FindFirstStringValue(root, candidateKeys);
    }

    /// <summary>
    /// Recursively walks the JSON tree, including wrapped "body" strings, looking for any
    /// candidate property name. Returns the first non-empty string/number value found.
    /// </summary>
    private static string? FindFirstStringValue(JsonElement element, IReadOnlyList<string> candidateKeys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                // Direct property match first - prefer shallow matches.
                foreach (var key in candidateKeys)
                {
                    if (element.TryGetProperty(key, out var prop))
                    {
                        var value = ToScalar(prop);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }

                // Special handling: a "body" string may itself be JSON, parse and search it.
                if (element.TryGetProperty("body", out var bodyProp) &&
                    bodyProp.ValueKind == JsonValueKind.String)
                {
                    var bodyString = bodyProp.GetString();
                    if (!string.IsNullOrWhiteSpace(bodyString))
                    {
                        JsonElement innerRoot;
                        try
                        {
                            using var innerDoc = JsonDocument.Parse(bodyString);
                            innerRoot = innerDoc.RootElement.Clone();
                        }
                        catch (JsonException)
                        {
                            // body is not JSON; ignore and continue searching sibling fields.
                            innerRoot = default;
                            goto RecurseSiblings;
                        }

                        var fromBody = FindFirstStringValue(innerRoot, candidateKeys);
                        if (!string.IsNullOrWhiteSpace(fromBody))
                        {
                            return fromBody;
                        }
                    }
                }

            RecurseSiblings:
                // Recurse into child properties to find deeply nested matches.
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("body"))
                    {
                        continue; // already handled above
                    }

                    var nested = FindFirstStringValue(prop.Value, candidateKeys);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }

                return null;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindFirstStringValue(item, candidateKeys);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }

                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Converts a JSON primitive into a string. Numbers are rendered with their raw text form
    /// (so "1640" stays "1640", not "1.640E+3"). Returns null for non-scalar values.
    /// </summary>
    private static string? ToScalar(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }
}