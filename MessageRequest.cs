using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NorthStar;

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class MessageRequest
{
    public uint Territory { get; set; }
    public uint? World { get; set; }
    public uint? Ward { get; set; }
    public uint? Plot { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public Guid PackId { get; set; }

    [JsonProperty("template_1")]
    public int Template1 { get; set; }

    [JsonProperty("word_1_list")]
    public int? Word1List { get; set; }

    [JsonProperty("word_1_word")]
    public int? Word1Word { get; set; }

    public int? Conjunction { get; set; }

    [JsonProperty("template_2")]
    public int? Template2 { get; set; }

    [JsonProperty("word_2_list")]
    public int? Word2List { get; set; }

    [JsonProperty("word_2_word")]
    public int? Word2Word { get; set; }

    public int Glyph { get; set; }
    public EmoteData? Emote { get; set; }
}