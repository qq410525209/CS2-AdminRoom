using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using CS2_AdminRoom.Models;
using System.Text.Json;
using ModelVector = CS2_AdminRoom.Models.Vector;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Events;
using WASDSharedAPI;
using CounterStrikeSharp.API.Core.Capabilities;

namespace CS2_AdminRoom
{
    [MinimumApiVersion(300)]
    public class CS2_AdminRoom : BasePlugin, IPluginConfig<CS2_AdminRoomConfig>
    {
        private static readonly string Prefix = $" {ChatColors.Blue}[AdminRoom]{ChatColors.Default}";

        public override string ModuleName => "CS2-AdminRoom Plugin";
        public override string ModuleDescription => "plugin for searching admin rooms, adding or deleting admin room coordinates";
        public override string ModuleAuthor => "小彩旗";
        public override string ModuleVersion => "1.0.1";

        public CS2_AdminRoomConfig Config { get; set; } = new();
        private string DataFilePath => Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", "CS2-AdminRoom", "adminroom.json");
        private AdminRoomData RoomData { get; set; } = new();
        private static IWasdMenuManager? MenuManager;

        public void OnConfigParsed(CS2_AdminRoomConfig config)
        {
            Config = config;
        }

        public override void Load(bool hotReload)
        {
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            LoadRoomData();

            AddCommand("css_addadminroom", "Add admin room at current position", CommandAddAdminRoom);
            AddCommand("css_adminroom", "Teleport to admin room", CommandAdminRoom);
            AddCommand("css_sadminroom", "Search and teleport to admin buttons", CommandSearchAdminRoom);
            AddCommand("css_deladminroom", "Delete admin room for current map", CommandDeleteAdminRoom);
        }

        private void LoadRoomData()
        {
            if (!File.Exists(DataFilePath))
            {
                SaveRoomData();
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(DataFilePath);
                RoomData = JsonSerializer.Deserialize<AdminRoomData>(jsonContent) ?? new AdminRoomData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading room data: {ex.Message}");
                RoomData = new AdminRoomData();
            }
        }

        private void SaveRoomData()
        {
            try
            {
                string jsonContent = JsonSerializer.Serialize(RoomData, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(DataFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving room data: {ex.Message}");
            }
        }

        private void OnMapStart(string mapName)
        {
            if (!RoomData.Maps.ContainsKey(mapName))
            {
                RoomData.Maps[mapName] = new MapData();
                SaveRoomData();
                Console.WriteLine($"Added new map {mapName} to room data");
            }

            // 如果启用了默认搜索
            if (Config.DefaultSearch.Enabled)
            {
                // 先检查是否有 NOAdminRoom 标记
                var mapData = RoomData.Maps[mapName];
                var existingRoom = mapData.Rooms.FirstOrDefault(r => r.Name == "NOAdminRoom");
                if (existingRoom != null)
                {
                    Console.WriteLine($"[AdminRoom] Map {mapName} is marked as NOAdminRoom, skipping search");
                    return;
                }

                // 检查是否已有 AdminRoom 标记
                var adminRoom = mapData.Rooms.FirstOrDefault(r => r.Name == "AdminRoom");
                if (adminRoom != null)
                {
                    Console.WriteLine($"[AdminRoom] Map {mapName} already has AdminRoom, skipping search");
                    return;
                }

                // 搜索按钮实体
                var buttons = Utilities.FindAllEntitiesByDesignerName<CBaseButton>("func_button");
                var matchedButtons = new List<CBaseButton>();

                foreach (var button in buttons)
                {
                    if (button == null || !button.IsValid || button.Entity == null || string.IsNullOrEmpty(button.Entity.Name)) continue;

                    string buttonName = button.Entity.Name;
                    if (Config.Keywords.Any(keyword => buttonName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        matchedButtons.Add(button);
                    }
                }

                if (matchedButtons.Count > 0)
                {
                    // 向有权限的玩家发送消息
                    foreach (var player in Utilities.GetPlayers())
                    {
                        if (player == null || !player.IsValid || !HasPermissions(player, Config.DefaultSearch.Permissions)) continue;
                        player.PrintToChat($"{Prefix} {Localizer["MapHasButtons", $"{ChatColors.Green}{matchedButtons.Count}{ChatColors.Default}"]}");
                    }
                }
                else
                {
                    // 如果没有找到按钮，添加 NOAdminRoom 标记
                    var roomData = new RoomData
                    {
                        Name = "NOAdminRoom"
                    };
                    mapData.Rooms.Add(roomData);
                    SaveRoomData();
                    Console.WriteLine($"[AdminRoom] No admin buttons found in {mapName}, marked as NOAdminRoom");
                }
            }
        }

        private void LogCommand(CCSPlayerController player, string command, string result)
        {
            Console.WriteLine($"[AdminRoom] Player {player.PlayerName} ({player.SteamID}) executed {command} - {result}");
        }

        private void CommandAddAdminRoom(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid) return;

            if (!HasPermissions(player, Config.Permissions.AddAdminRoom))
            {
                LogCommand(player, "css_addadminroom", "Permission Denied");
                player.PrintToChat($"{Prefix} {Localizer["NoPermission"]}");
                return;
            }

            string mapName = Server.MapName;
            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.AbsOrigin == null)
            {
                LogCommand(player, "css_addadminroom", "Failed - Invalid Position");
                player.PrintToChat($"{Prefix} {Localizer["NoPosition"]}");
                return;
            }

            var position = pawn.AbsOrigin!;

            // 创建新的房间数据
            var roomData = new RoomData
            {
                Name = "AdminRoom",
                Position = new ModelVector
                {
                    X = position.X,
                    Y = position.Y,
                    Z = position.Z
                }
            };

            // 确保地图存在
            if (!RoomData.Maps.ContainsKey(mapName))
            {
                RoomData.Maps[mapName] = new MapData();
            }

            // 添加或更新房间
            var mapData = RoomData.Maps[mapName];
            var existingRoom = mapData.Rooms.FirstOrDefault(r => r.Name == "AdminRoom");
            if (existingRoom != null)
            {
                existingRoom.Position = roomData.Position;
            }
            else
            {
                mapData.Rooms.Add(roomData);
            }

            SaveRoomData();
            LogCommand(player, "css_addadminroom", $"Success - Set room at {pawn.AbsOrigin}");
            player.PrintToChat($"{Prefix} {Localizer["RoomSet"]}");
        }

        private void CommandAdminRoom(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid) return;

            if (!HasPermissions(player, Config.Permissions.UseAdminRoom))
            {
                LogCommand(player, "css_adminroom", "Permission Denied");
                player.PrintToChat($"{Prefix} {Localizer["NoPermission"]}");
                return;
            }

            string mapName = Server.MapName;
            if (!RoomData.Maps.ContainsKey(mapName))
            {
                player.PrintToChat($"{Prefix} {Localizer["NoRoom"]}");
                return;
            }

            var mapData = RoomData.Maps[mapName];
            var adminRoom = mapData.Rooms.FirstOrDefault(r => r.Name == "AdminRoom");
            if (adminRoom == null)
            {
                player.PrintToChat($"{Prefix} {Localizer["NoRoom"]}");
                return;
            }

            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid) return;

            // 传送玩家
            pawn.Teleport(
                new CounterStrikeSharp.API.Modules.Utils.Vector(adminRoom.Position.X, adminRoom.Position.Y, adminRoom.Position.Z),
                new QAngle(0, 0, 0),
                new CounterStrikeSharp.API.Modules.Utils.Vector(0, 0, 0)
            );

            player.PrintToChat($"{Prefix} {Localizer["RoomTeleported"]}");
        }

        private IWasdMenuManager? GetMenuManager()
        {
            if (MenuManager == null)
                MenuManager = new PluginCapability<IWasdMenuManager>("wasdmenu:manager").Get();
            return MenuManager;
        }

        private void CommandSearchAdminRoom(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid) return;

            if (!HasPermissions(player, Config.Permissions.SearchAdminRoom))
            {
                LogCommand(player, "css_sadminroom", "Permission Denied");
                player.PrintToChat($"{Prefix} {Localizer["NoPermission"]}");
                return;
            }

            var manager = GetMenuManager();
            if (manager == null) 
            {
                LogCommand(player, "css_sadminroom", "Failed - WASDMenu Not Found");
                player.PrintToChat($"{Prefix} {Localizer["NoWASDMenu"]}");
                return;
            }

            // 搜索按钮实体
            var buttons = Utilities.FindAllEntitiesByDesignerName<CBaseButton>("func_button");
            var matchedButtons = new List<CBaseButton>();

            foreach (var button in buttons)
            {
                if (button == null || !button.IsValid || button.Entity == null || string.IsNullOrEmpty(button.Entity.Name)) continue;

                string buttonName = button.Entity.Name;
                if (Config.Keywords.Any(keyword => buttonName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedButtons.Add(button);
                }
            }

            if (matchedButtons.Count == 0)
            {
                LogCommand(player, "css_sadminroom", "Failed - No Buttons Found");
                player.PrintToChat($"{Prefix} {Localizer["NoButtonsFound"]}");
                return;
            }

            LogCommand(player, "css_sadminroom", $"Success - Found {matchedButtons.Count} buttons");
            player.PrintToChat($"{Prefix} {Localizer["ButtonsFound", $"{ChatColors.Green}{matchedButtons.Count}{ChatColors.Default}"]}");

            // 创建菜单
            IWasdMenu menu = manager.CreateMenu($"管理员按钮列表 (共{matchedButtons.Count}个)");

            // 添加按钮选项
            foreach (var button in matchedButtons)
            {
                if (button == null || !button.IsValid || button.Entity == null) continue;
                
                menu.Add(button.Entity.Name, (p, option) =>
                {
                    if (p == null || !p.IsValid || p.PlayerPawn == null) return;
                    var pawn = p.PlayerPawn.Value;
                    if (pawn == null || !pawn.IsValid) return;

                    // 传送玩家到按钮位置
                    if (button.AbsOrigin == null) return;
                    var position = button.AbsOrigin;
                    position.Z += Config.TeleportHeight;
                    pawn.Teleport(
                        position,
                        new QAngle(0, 0, 0),
                        new CounterStrikeSharp.API.Modules.Utils.Vector(0, 0, 0)
                    );

                    LogCommand(p, "css_sadminroom", $"Success - Teleported to button {button.Entity.Name}");
                    p.PrintToChat($"{Prefix} {Localizer["ButtonTeleported", $"{ChatColors.Green}{button.Entity.Name}{ChatColors.Default}"]}");
                    manager.CloseMenu(p);
                });
            }

            // 显示菜单
            manager.OpenMainMenu(player, menu);
        }

        private void CommandDeleteAdminRoom(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid) return;

            if (!HasPermissions(player, Config.Permissions.DeleteAdminRoom))
            {
                LogCommand(player, "css_deladminroom", "Permission Denied");
                player.PrintToChat($"{Prefix} {Localizer["NoPermission"]}");
                return;
            }

            string mapName = Server.MapName;
            if (!RoomData.Maps.ContainsKey(mapName))
            {
                LogCommand(player, "css_deladminroom", "Failed - No Room Found");
                player.PrintToChat($"{Prefix} {Localizer["NoRoom"]}");
                return;
            }

            RoomData.Maps.Remove(mapName);
            SaveRoomData();
            LogCommand(player, "css_deladminroom", $"Success - Deleted room for map {mapName}");
            player.PrintToChat($"{Prefix} {Localizer["RoomDeleted"]}");
        }

        public override void Unload(bool hotReload)
        {
        }

        private bool HasPermissions(CCSPlayerController player, List<string> permissions)
        {
            foreach (string permission in permissions)
            {
                if (AdminManager.PlayerHasPermissions(player, permission))
                {
                    return true;
                }
            }
            return false;
        }
    }
}