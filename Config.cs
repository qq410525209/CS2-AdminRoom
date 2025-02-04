using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace CS2_AdminRoom
{
    public class CS2_AdminRoomConfig : BasePluginConfig
    {

        [JsonPropertyName("Keywords")]
        public List<string> Keywords { get; set; } = new()
        {
            "admin",
            "stage",
            "level",
            "lvl",
            "act",
            "extreme",
            "ex1",
            "ex2",
            "ex3",
            "ex4",
            "round",
            "kill",
            "restart"
        };

        [JsonPropertyName("ButtonTypes")]
        public List<string> ButtonTypes { get; set; } = new()
        {
            "func_button",
            "func_physical_button",
            "func_rot_button"
        };

        [JsonPropertyName("Permissions")]
        public PermissionsConfig Permissions { get; set; } = new();

        [JsonPropertyName("TeleportHeight")]
        public float TeleportHeight { get; set; } = 20.0f;

        [JsonPropertyName("DefaultSearch")]
        public DefaultSearchConfig DefaultSearch { get; set; } = new();
    }

    public class PermissionsConfig
    {
        [JsonPropertyName("AddAdminRoom")]
        public List<string> AddAdminRoom { get; set; } = new() { "@css/root" };

        [JsonPropertyName("UseAdminRoom")]
        public List<string> UseAdminRoom { get; set; } = new() { "@css/root", "@css/admin" };

        [JsonPropertyName("SearchAdminRoom")]
        public List<string> SearchAdminRoom { get; set; } = new() { "@css/root", "@css/admin" };

        [JsonPropertyName("DeleteAdminRoom")]
        public List<string> DeleteAdminRoom { get; set; } = new() { "@css/root" };
    }

    public class DefaultSearchConfig
    {
        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("Permissions")]
        public List<string> Permissions { get; set; } = new() { "@css/root", "@css/admin" };
    }
}
