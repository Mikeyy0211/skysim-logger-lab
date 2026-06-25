using System.Text.Json;

namespace Skysim.Logger.Common.Masking;

public interface ISensitiveDataMasker
{
    string MaskJson(string json);
}

public class SensitiveDataMasker : ISensitiveDataMasker
{
    private const string MaskedValue = "***";

    public string MaskJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var masked = MaskElement(doc.RootElement);
            return masked.GetRawText();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static JsonElement MaskElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => MaskObject(element),
            JsonValueKind.Array => MaskArray(element),
            _ => element
        };
    }

    private static JsonElement MaskObject(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        foreach (var property in element.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);

            if (SensitiveFields.Instance.IsSensitive(property.Name))
            {
                writer.WriteStringValue(MaskedValue);
            }
            else
            {
                MaskElement(property.Value).WriteTo(writer);
            }
        }

        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }

    private static JsonElement MaskArray(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();

        foreach (var item in element.EnumerateArray())
        {
            MaskElement(item).WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }
}
