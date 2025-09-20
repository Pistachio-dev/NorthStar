using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OrangeGuidanceTomestone;

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class MessageRequest {
    public required uint Territory { get; set; }
    public required uint? World { get; set; }
    public required uint? Ward { get; set; }
    public required uint? Plot { get; set; }
    public required float X { get; set; }
    public required float Y { get; set; }
    public required float Z { get; set; }
    public required float Yaw { get; set; }
    public required Guid PackId { get; set; }

    [JsonProperty("template_1")]
    public required int Template1 { get; set; }

    [JsonProperty("word_1_list")]
    public required int? Word1List { get; set; }

    [JsonProperty("word_1_word")]
    public required int? Word1Word { get; set; }

    public required int? Conjunction { get; set; }

    [JsonProperty("template_2")]
    public required int? Template2 { get; set; }

    [JsonProperty("word_2_list")]
    public required int? Word2List { get; set; }

    [JsonProperty("word_2_word")]
    public required int? Word2Word { get; set; }

    public required int Glyph { get; set; }
    public required EmoteData? Emote { get; set; }
}
