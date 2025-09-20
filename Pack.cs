using NorthStar.Helpers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NorthStar;

[Serializable]
public class Pack
{
    internal static SemaphoreSlim AllMutex { get; } = new(1, 1);
    internal static Pack[] All { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = {
            new TemplateConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public string Name { get; init; }
    public Guid Id { get; init; }

    public Template[] Templates { get; init; }

    public string[]? Conjunctions { get; init; }
    public List<WordList>? Words { get; init; }

    internal static void UpdatePacks()
    {
        Task.Run(async () =>
        {
            var resp = await ServerHelper.SendRequest(null, HttpMethod.Get, "/packs");
            var json = await resp.Content.ReadAsStringAsync();
            var packs = JsonSerializer.Deserialize<Pack[]>(json, Options)!;
            await AllMutex.WaitAsync();
            try
            {
                All = packs;
            }
            finally
            {
                AllMutex.Release();
            }
        });
    }
}

public class Template
{
    [JsonPropertyName("template")]
    public string Text { get; init; }

    public string[]? Words { get; init; }
}

public class TemplateConverter : JsonConverter<Template>
{
    private static JsonSerializerOptions RemoveSelf(JsonSerializerOptions old)
    {
        var newOptions = new JsonSerializerOptions(old);
        for (var i = 0; i < old.Converters.Count; i++)
        {
            if (old.Converters[i] is TemplateConverter)
            {
                newOptions.Converters.RemoveAt(i);
                break;
            }
        }

        return newOptions;
    }

    public override Template? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                {
                    var template = reader.GetString() ?? throw new JsonException("template cannot be null");
                    return new Template
                    {
                        Text = template,
                        Words = null,
                    };
                }
            case JsonTokenType.StartObject:
                {
                    var newOptions = RemoveSelf(options);
                    return JsonSerializer.Deserialize<Template>(ref reader, newOptions);
                }
            default:
                {
                    throw new JsonException("unexpected template type");
                }
        }
    }

    public override void Write(Utf8JsonWriter writer, Template value, JsonSerializerOptions options)
    {
        if (value.Words == null)
        {
            JsonSerializer.Serialize(writer, value.Text, options);
        }
        else
        {
            var newOptions = RemoveSelf(options);
            JsonSerializer.Serialize(writer, value, newOptions);
        }
    }
}

[Serializable]
public class WordList
{
    public string Name { get; init; }
    public string[] Words { get; init; }
}