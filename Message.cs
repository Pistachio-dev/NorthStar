using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Numerics;

namespace NorthStar;

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class Message
{
    public Guid Id { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }

    [JsonProperty("message")]
    public string Text { get; init; }

    public int PositiveVotes { get; set; }
    public int NegativeVotes { get; set; }
    public int UserVote { get; set; }

    public EmoteData? Emote { get; set; }

    public int Glyph { get; set; }

    internal Vector3 Position => new(X, Y, Z);
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class MessageWithTerritory
{
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

    internal Vector3 Position => new(X, Y, Z);

    internal static MessageWithTerritory From(Message message, uint territory)
    {
        return new MessageWithTerritory
        {
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
public class EmoteData
{
    public uint Id { get; set; }
    public List<byte> Customise { get; set; }
    public EquipmentData[] Equipment { get; set; }
    public WeaponData[] Weapon { get; set; }
    public uint Glasses { get; set; }
    public bool HatHidden { get; set; }
    public bool VisorToggled { get; set; }
    public bool WeaponHidden { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class EquipmentData
{
    public ushort Id { get; set; }
    public byte Variant { get; set; }

    [JsonProperty("stain_0")]
    public byte Stain0 { get; set; }

    [JsonProperty("stain_1")]
    public byte Stain1 { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class WeaponData
{
    public WeaponModelId ModelId { get; set; }
    public byte State { get; set; }

    [JsonProperty("flags_1")]
    public ushort Flags1 { get; set; }

    [JsonProperty("flags_2")]
    public byte Flags2 { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class WeaponModelId
{
    public ushort Id { get; set; }
    public ushort Kind { get; set; }
    public ushort Variant { get; set; }

    [JsonProperty("stain_0")]
    public byte Stain0 { get; set; }

    [JsonProperty("stain_1")]
    public byte Stain1 { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class ErrorMessage
{
    public string Code { get; set; }
    public string Message { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class MyMessages
{
    public uint Extra { get; set; }
    public MessageWithTerritory[] Messages { get; set; }
}