using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OrangeGuidanceTomestone;

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class Message {
    public required Guid Id { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Z { get; init; }
    public required float Yaw { get; init; }

    [JsonProperty("message")]
    public required string Text { get; init; }

    public required int PositiveVotes { get; set; }
    public required int NegativeVotes { get; set; }
    public required int UserVote { get; set; }

    public required EmoteData? Emote { get; set; }

    public required int Glyph { get; set; }

    internal Vector3 Position => new(this.X, this.Y, this.Z);
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class MessageWithTerritory {
    public Guid Id { get; init; }
    public uint Territory { get; init; }
    public uint? Ward { get; init; }
    public uint? Plot { get; init; }
    public uint? World { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }

    [JsonProperty("message")]
    public string Text { get; init; }

    public int PositiveVotes { get; init; }
    public int NegativeVotes { get; init; }
    public int UserVote { get; set; }

    public EmoteData? Emote { get; set; }

    public int Glyph { get; set; }
    public bool IsHidden { get; set; }

    internal Vector3 Position => new(this.X, this.Y, this.Z);

    internal static MessageWithTerritory From(Message message, uint territory) {
        return new MessageWithTerritory {
            Id = message.Id,
            Territory = territory,
            X = message.X,
            Y = message.Y,
            Z = message.Z,
            Yaw = message.Yaw,
            Text = message.Text,
            PositiveVotes = message.PositiveVotes,
            NegativeVotes = message.NegativeVotes,
            UserVote = message.UserVote,
            Emote = message.Emote,
            Glyph = message.Glyph,
            IsHidden = false,
        };
    }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class EmoteData {
    public required uint Id { get; set; }
    public required List<byte> Customise { get; set; }
    public required EquipmentData[] Equipment { get; set; }
    public required WeaponData[] Weapon { get; set; }
    public required uint Glasses { get; set; }
    public required bool HatHidden { get; set; }
    public required bool VisorToggled { get; set; }
    public required bool WeaponHidden { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class EquipmentData {
    public required ushort Id { get; set; }
    public required byte Variant { get; set; }
    [JsonProperty("stain_0")]
    public required byte Stain0 { get; set; }
    [JsonProperty("stain_1")]
    public required byte Stain1 { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class WeaponData {
    public required WeaponModelId ModelId { get; set; }
    public required byte State { get; set; }
    [JsonProperty("flags_1")]
    public required ushort Flags1 { get; set; }
    [JsonProperty("flags_2")]
    public required byte Flags2 { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class WeaponModelId {
    public required ushort Id { get; set; }
    public required ushort Kind { get; set; }
    public required ushort Variant { get; set; }
    [JsonProperty("stain_0")]
    public required byte Stain0 { get; set; }
    [JsonProperty("stain_1")]
    public required byte Stain1 { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class ErrorMessage {
    public string Code { get; set; }
    public string Message { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class MyMessages {
    public uint Extra { get; set; }
    public MessageWithTerritory[] Messages { get; set; }
}
