using System.Text.Json.Serialization;

namespace CS2_AdminRoom.Models
{
    public class AdminRoomData
    {
        [JsonPropertyName("Maps")]
        public Dictionary<string, MapData> Maps { get; set; } = new Dictionary<string, MapData>();
    }

    public class MapData
    {
        [JsonPropertyName("Rooms")]
        public List<RoomData> Rooms { get; set; } = new List<RoomData>();
    }

    public class RoomData
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Position")]
        public Vector Position { get; set; } = new Vector();
    }

    public class Vector
    {
        [JsonPropertyName("X")]
        public float X { get; set; }

        [JsonPropertyName("Y")]
        public float Y { get; set; }

        [JsonPropertyName("Z")]
        public float Z { get; set; }
    }
} 