using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Radar", "Austinv900 & Speedy2M", "3.0.0", ResourceId = 978)]
    [Description("Allows admins to have a radar to help detect cheaters")]
    public class AdminRadar : RustPlugin
    {
        #region Libraries

        [Flags]
        public enum RadarFilter
        {
            None = 0,
            Player = 1,
            Sleeper = 2,
            NPC = 4,
            ToolCupboard = 8,
            Container = 16,
            ResourceNode = 32,
            All = Player | Sleeper | NPC | ToolCupboard | Container | ResourceNode
        }

        private List<string> FilterList = new List<string>();
        private List<string> ActiveRadars = new List<string>();

        #endregion

        #region Radar Class

        public class Radar : MonoBehaviour
        {
            private static Color GetColor(string ConfigVariable)
            {
                var rgba = Array.ConvertAll(ConfigVariable.Split(','), float.Parse);
                return new Color(rgba[0], rgba[1], rgba[2]);
            }

            // Static Variables
            private static readonly Covalence covalence = Interface.Oxide.GetLibrary<Covalence>();

            private static readonly Game.Rust.Libraries.Rust rust = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Rust>();
            private BasePlayer player;

            private Vector3 bodyheight = new Vector3(0f, 0.9f, 0f);
            private int arrowheight = 15;
            private int arrowsize = 1;
            private Vector3 textheight = new Vector3(0f, 0.0f, 0f);

            private class EntityList
            {
                public List<BasePlayer> ActivePlayerList { get; set; }

                public List<BasePlayer> SleepingPlayerList { get; set; }

                public List<BaseNpc> NPCList { get; set; }

                public List<BuildingPrivlidge> TCList { get; set; }

                public List<StorageContainer> ContainerList { get; set; }

                public List<ResourceDispenser> NodeList { get; set; }

                public bool Reloading = false;

                public void UpdateLists(RadarFilter Filter)
                {
                    Reloading = true;

                    if (HasFlag(Filter, RadarFilter.Player))
                        ActivePlayerList = BasePlayer.activePlayerList;

                    if (HasFlag(Filter, RadarFilter.Sleeper))
                        SleepingPlayerList = BasePlayer.sleepingPlayerList;

                    if (HasFlag(Filter, RadarFilter.NPC))
                        NPCList = GameObject.FindObjectsOfType<BaseNpc>().Where(n => n.transform.position != Vector3.zero).ToList();

                    if (HasFlag(Filter, RadarFilter.ToolCupboard))
                        TCList = GameObject.FindObjectsOfType<BuildingPrivlidge>().Where(t => t.transform.position != Vector3.zero).ToList();

                    if (HasFlag(Filter, RadarFilter.Container))
                        ContainerList = GameObject.FindObjectsOfType<StorageContainer>().Where(c => c.transform.position != Vector3.zero && c.inventorySlots > 3).ToList();

                    if (HasFlag(Filter, RadarFilter.ResourceNode))
                        NodeList = GameObject.FindObjectsOfType<ResourceDispenser>().Where(c => c.transform.position != Vector3.zero).ToList();
                    Reloading = false;
                }

                public void RemoveNullEnt(BaseNetworkable networkable)
                {
                    if (networkable is BaseNpc)
                        NPCList.Remove(networkable as BaseNpc);
                    if (networkable is BuildingPrivlidge)
                        TCList.Remove(networkable as BuildingPrivlidge);
                    if (networkable is StorageContainer)
                        ContainerList.Remove(networkable as StorageContainer);
                }
            }

            private Dictionary<string, string> ExtMessages = new Dictionary<string, string>()
            {
                ["player"] = "{0} - |H: {1}|CW: {2}|AT: {3}|D: {4}m",
                ["sleeper"] = "{0}(<color=red>Sleeping</color>) - |H: {1}|D: {2}m",
                ["thing"] = "{0}{1} - |D: {2}m",
                ["npc"] = "{0} - |H: {1}|D: {2}m"
            };

            private Dictionary<string, string> Messages = new Dictionary<string, string>()
            {
                ["player"] = "{0} - |D: {4}m",
                ["sleeper"] = "{0}(<color=red>Sleeping</color>) - |D: {2}m",
                ["thing"] = "{0}{1} - |D: {2}m",
                ["npc"] = "{0} - |D: {2}m"
            };

            // Changable Variables

            public RadarFilter filter;

            private EntityList list { get; set; }

            private void UpdateEntList()
            {
                if (list == null)
                    list = new EntityList();
                list.UpdateLists(filter);
            }

            void Awake()
            {
                player = GetComponent<BasePlayer>();
            }

            public void InitRadar()
            {
                if (list == null)
                    list = new EntityList();

                InvokeRepeating(nameof(UpdateEntList), 0, 60);

                if (Configuration.Filter_Player && HasFlag(filter, RadarFilter.Player))
                    InvokeRepeating(nameof(ConnectedPlayerInvoke), 1, Configuration.RefreshRate_Player);
                if (Configuration.Filter_Sleeper && HasFlag(filter, RadarFilter.Sleeper))
                    InvokeRepeating(nameof(SleepingPlayerInvoke), 1, Configuration.RefreshRate_Sleeper);
                if (Configuration.Filter_NPC && HasFlag(filter, RadarFilter.NPC))
                    InvokeRepeating(nameof(NPCInvoke), 1, Configuration.RefreshRate_NPC);
                if (Configuration.Filter_Container && HasFlag(filter, RadarFilter.Container))
                    InvokeRepeating(nameof(ContainerInvoke), 1, Configuration.RefreshRate_Container);
                if (Configuration.Filter_BuildingPrivledge && HasFlag(filter, RadarFilter.ToolCupboard))
                    InvokeRepeating(nameof(ToolCupboardInvoke), 1, Configuration.RefreshRate_BuildingPrivledge);
                if (Configuration.Filter_Node && HasFlag(filter, RadarFilter.ResourceNode))
                    InvokeRepeating(nameof(NodeInvoke), 1, Configuration.RefreshRate_Node);
                enabled = true;
            }

            void ConnectedPlayerInvoke()
            {
                if (list.Reloading)
                    return;
                string message = (Configuration.ShowExtendedData) ? ExtMessages["player"] : Messages["player"];
                foreach (var target in list.ActivePlayerList)
                {
                    if (target == null)
                    {
                        continue;
                    }
                    var distance = Vector3.Distance(target.transform.position, player.transform.position);
                    if (distance < Configuration.ViewDistance_Player && target != player)
                    {
                        var health = Math.Round(target.Health(), 0).ToString();
                        var cw = target?.GetActiveItem()?.info?.displayName?.english ?? "None";
                        var weapon = target?.GetHeldEntity()?.GetComponent<BaseProjectile>() ?? null;
                        var attachments = string.Empty;
                        var contents = weapon?.GetItem()?.contents ?? null;
                        if (weapon != null && contents != null && contents.itemList.Count >= 1)
                        {
                            attachments += "";
                            for (int ii = 0; ii < contents.itemList.Count; ii++)
                            {
                                var item = contents.itemList[ii];
                                if (item == null) continue;
                                attachments += item?.info?.displayName?.english ?? "None";
                            }
                            attachments += "";
                        }
                        var msg = message.Replace("{0}", target.displayName).Replace("{1}", health).Replace("{2}", cw).Replace("{3}", attachments).Replace("{4}", distance.ToString("0.00"));

                        if (Configuration.RadarBoxes) player.SendConsoleCommand("ddraw.box", Configuration.RefreshRate_Player, GetColor(Configuration.BoxColor_Player), target.transform.position + bodyheight, target.GetHeight());
                        if (Configuration.RadarText) player.SendConsoleCommand("ddraw.text", Configuration.RefreshRate_Player, GetColor(Configuration.TextColor_Player), target.transform.position + textheight, msg);
                    }
                }
            }

            void SleepingPlayerInvoke()
            {
                if (list.Reloading)
                    return;

                string message = (Configuration.ShowExtendedData) ? ExtMessages["sleeper"] : Messages["sleeper"];
                foreach (var sleeper in list.SleepingPlayerList)
                {
                    if (sleeper == null)
                    {
                        continue;
                    }
                    var distance = Vector3.Distance(sleeper.transform.position, player.transform.position);
                    var msg = message.Replace("{0}", sleeper.displayName).Replace("{1}", Math.Round(sleeper.Health(), 0).ToString()).Replace("{2}", distance.ToString("0.00"));
                    if (distance < Configuration.ViewDistance_Sleeper)
                    {
                        if (Configuration.RadarText) player.SendConsoleCommand("ddraw.text", Configuration.RefreshRate_Sleeper, GetColor(Configuration.TextColor_Sleeper), sleeper.transform.position + textheight, msg);
                    }
                }
            }

            void ToolCupboardInvoke()
            {
                if (list.Reloading)
                    return;
                string message = (Configuration.ShowExtendedData) ? ExtMessages["thing"] : Messages["thing"];
                foreach (var Cupboard in list.TCList.ToList())
                {
                    try
                    {
                        if (Cupboard == null)
                        {
                            continue;
                        }
                        var distance = Vector3.Distance(Cupboard.transform.position, player.transform.position);
                        if (distance < Configuration.ViewDistance_BuildingPrivledge)
                        {
                            var arrowSky = Cupboard.transform.position;
                            var arrowGround = arrowSky + new Vector3(0, 0.9f, 0);
                            arrowGround.y = arrowGround.y + arrowheight;
                            var owner = FindOwner(Cupboard.OwnerID);
                            var msg = message.Replace("{0}", replacement(Cupboard.ShortPrefabName)).Replace("{1}", $"[{owner}]").Replace("{2}", distance.ToString("0.00"));

                            if (Configuration.RadarArrows) player.SendConsoleCommand("ddraw.arrow", Configuration.RefreshRate_BuildingPrivledge, GetColor(Configuration.ArrowColor_BuildingPrivledge), arrowGround, arrowSky, arrowsize);
                            if (Configuration.RadarText) player.SendConsoleCommand("ddraw.text", Configuration.RefreshRate_BuildingPrivledge, GetColor(Configuration.TextColor_BuildingPrivledge), Cupboard.transform.position + new Vector3(0f, 0.05f, 0f), msg);
                        }
                    }
                    catch
                    {
                        list.RemoveNullEnt(Cupboard);
                    }
                }
            }

            void ContainerInvoke()
            {
                if (list.Reloading)
                    return;
                string message = (Configuration.ShowExtendedData) ? ExtMessages["thing"] : Messages["thing"];
                foreach (var storage in list.ContainerList.ToList())
                {
                    try
                    {
                        if (storage == null)
                            continue;
                        var distance = Vector3.Distance(storage.transform.position, player.transform.position);
                        if (distance < Configuration.ViewDistance_Container)
                        {
                            var owner = FindOwner(storage.OwnerID);
                            var arrowSky = storage.transform.position;
                            var arrowGround = arrowSky;
                            var msg = message.Replace("{0}", replacement(storage.ShortPrefabName)).Replace("{1}", (owner == "Null") ? string.Empty : $"[{owner}]").Replace("{2}", distance.ToString("0.00"));
                            arrowGround.y = arrowGround.y + arrowheight;

                            if (Configuration.RadarArrows) player.SendConsoleCommand("ddraw.arrow", Configuration.RefreshRate_Container, GetColor(Configuration.ArrowColor_Container), arrowGround, arrowSky, arrowsize);
                            if (Configuration.RadarText) player.SendConsoleCommand("ddraw.text", Configuration.RefreshRate_Container, GetColor(Configuration.TextColor_Container), storage.transform.position + new Vector3(0f, 0.05f, 0f), msg);
                        }
                    }
                    catch
                    {
                        list.RemoveNullEnt(storage);
                    }
                }
            }

            void NPCInvoke()
            {
                if (list.Reloading)
                    return;
                string message = (Configuration.ShowExtendedData) ? ExtMessages["npc"] : Messages["npc"];
                foreach (var npc in list.NPCList.ToList())
                {
                    try
                    {
                        if (npc == null)
                            continue;
                        var distance = Vector3.Distance(npc.transform.position, player.transform.position);
                        if (distance < Configuration.ViewDistance_NPC)
                        {
                            var health = Math.Round(npc.Health(), 0).ToString();
                            var msg = message.Replace("{0}", npc.ShortPrefabName.Replace(".prefab", string.Empty)).Replace("{1}", health).Replace("{2}", distance.ToString("0.00"));

                            if (Configuration.RadarText) player.SendConsoleCommand("ddraw.text", Configuration.RefreshRate_NPC, GetColor(Configuration.TextColor_NPC), npc.transform.position + textheight, msg);
                        }
                    }
                    catch
                    {
                        list.RemoveNullEnt(npc);
                    }
                }
            }

            void NodeInvoke()
            {
                if (list.Reloading)
                    return;
                string message = (Configuration.ShowExtendedData) ? ExtMessages["thing"] : Messages["thing"];
                foreach (var node in list.NodeList.ToList())
                {
                    try
                    {
                        var distance = Vector3.Distance(node.transform.position, player.transform.position);
                        if (distance < Configuration.ViewDistance_Node)
                        {
                            var arrowSky = node.transform.position;
                            var arrowGround = arrowSky;
                            var msg = message.Replace("{0}", replacement(node.gatherType.ToString())).Replace("{1}", $"[World]").Replace("{2}", distance.ToString("0.00"));
                            arrowGround.y = arrowGround.y + arrowheight;

                            if (Configuration.RadarArrows) player.SendConsoleCommand("ddraw.arrow", Configuration.RefreshRate_Node, GetColor(Configuration.ArrowColor_Nodes), arrowGround, arrowSky, arrowsize);
                            if (Configuration.RadarText) player.SendConsoleCommand("ddraw.text", Configuration.RefreshRate_Node, GetColor(Configuration.TextColor_Nodes), node.transform.position + new Vector3(0f, 0.05f, 0f), msg);
                        }
                    }
                    catch
                    {
                        list.NodeList.Remove(node);
                    }
                }
            }

            string FindOwner(ulong id)
            {
                return covalence.Players.FindPlayerById(id.ToString())?.Name ?? "Null";
            }

            bool SpectateCheck(BasePlayer player, BasePlayer target) => player.IsSpectating() && target.HasChild(player);

            string replacement(string name)
            {
                return name.Replace(".prefab", string.Empty).Replace(".wooden.", string.Empty).Replace("_deployed", string.Empty).Replace("small_", string.Empty).Replace("_deployed", string.Empty).Replace(".tool.deployed", string.Empty).Replace("_", " ").ToUpper();
            }
        }

        #endregion

        #region Oxide

        void OnServerInitialized()
        {
            cmd.AddChatCommand(Configuration.Command, this, nameof(ccmdRadar));
        }

        void Loaded()
        {
            LoadConfig();
            LoadFilterList();
            LoadMessages();
        }

        void Unload()
        {
            foreach (var pl in BasePlayer.activePlayerList)
            {
                if (pl.GetComponent<Radar>()) GameObject.Destroy(pl.GetComponent<Radar>());
                if (ActiveRadars.Contains(pl.UserIDString)) ActiveRadars.Remove(pl.UserIDString);
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player.GetComponent<Radar>()) { GameObject.Destroy(player.GetComponent<Radar>()); if (ActiveRadars.Contains(player.UserIDString)) ActiveRadars.Remove(player.UserIDString); }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Hold Configuration Values
        /// </summary>
        public static class Configuration
        {
            #region General Configuration

            /// <summary>
            /// SteamId of desired icon
            /// </summary>
            public static string IconProfile = "0";

            /// <summary>
            /// The Prefix that shows in chat
            /// </summary>
            public static string ChatPrefix = "AdminRadar";

            /// <summary>
            /// The command used to call the radar to life
            /// </summary>
            public static string Command = "radar";

            #endregion

            #region Filter Toggles

            /// <summary>
            /// Enables the All Filter
            /// </summary>
            public static bool Filter_All = false;

            /// <summary>
            /// Enables the Player Filter
            /// </summary>
            public static bool Filter_Player = true;

            /// <summary>
            /// Enables the Container Filter
            /// </summary>
            public static bool Filter_Container = true;

            /// <summary>
            /// Enables the Sleeper Filter
            /// </summary>
            public static bool Filter_Sleeper = true;

            /// <summary>
            /// Enables the BuildingPrivledge Filter
            /// </summary>
            public static bool Filter_BuildingPrivledge = true;

            /// <summary>
            /// Enables the NPC Filter
            /// </summary>
            public static bool Filter_NPC = true;

            /// <summary>
            /// Enables the Node Filter
            /// </summary>
            public static bool Filter_Node = true;

            #endregion

            #region Default Filter Settings

            /// <summary>
            /// Default Filter
            /// </summary>
            public static string DefaultSelectedFilter = "Player";

            /// <summary>
            /// Default Player RefreshRate
            /// </summary>
            public static float RefreshRate_Player = 1.0f;

            /// <summary>
            /// Default Sleeper RefreshRate
            /// </summary>
            public static float RefreshRate_Sleeper = 10.0f;

            /// <summary>
            /// Default Container RefreshRate
            /// </summary>
            public static float RefreshRate_Container = 10.0f;

            /// <summary>
            /// Default BuildingPrivledge RefreshRate
            /// </summary>
            public static float RefreshRate_BuildingPrivledge = 10.0f;

            /// <summary>
            /// Default NPC RefreshRate
            /// </summary>
            public static float RefreshRate_NPC = 2.0f;

            /// <summary>
            /// Default Node RefreshRate
            /// </summary>
            public static float RefreshRate_Node = 10.0f;

            /// <summary>
            /// Default Player Distance
            /// </summary>
            public static float ViewDistance_Player = 800f;

            /// <summary>
            /// Default Sleeper Distance
            /// </summary>
            public static float ViewDistance_Sleeper = 200f;

            /// <summary>
            /// Default Container Distance
            /// </summary>
            public static float ViewDistance_Container = 300f;

            /// <summary>
            /// Default BuildingPrivledge Distance
            /// </summary>
            public static float ViewDistance_BuildingPrivledge = 300f;

            /// <summary>
            /// Default NPC Distance
            /// </summary>
            public static float ViewDistance_NPC = 150f;

            /// <summary>
            /// Default Node Distance
            /// </summary>
            public static float ViewDistance_Node = 250f;

            #endregion

            #region Radar Options

            /// <summary>
            /// Shows more detailed status during radar
            /// </summary>
            public static bool ShowExtendedData = true;

            /// <summary>
            /// Enables/Disables Radar Boxes
            /// </summary>
            public static bool RadarBoxes = true;

            /// <summary>
            /// Radar Box Color
            /// </summary>
            public static string BoxColor_Player = "0,0.255,0";

            /// <summary>
            /// Enables/Disables Radar Arrows
            /// </summary>
            public static bool RadarArrows = true;

            /// <summary>
            /// Sets Arrow Color for Container
            /// </summary>
            public static string ArrowColor_Container = "0,0,0.255";

            /// <summary>
            /// Sets Arrow Color for BuildingPrivledge
            /// </summary>
            public static string ArrowColor_BuildingPrivledge = "0.255,0.255,0";

            /// <summary>
            /// Sets Arrow Color for ResourceNodes
            /// </summary>
            public static string ArrowColor_Nodes = "0.255,0.255,0";

            /// <summary>
            /// Enables/Disables Radar Text
            /// </summary>
            public static bool RadarText = true;

            /// <summary>
            /// Sets Text Color for Player
            /// </summary>
            public static string TextColor_Player = "1,1,0";

            /// <summary>
            /// Sets Text Color for Sleeper
            /// </summary>
            public static string TextColor_Sleeper = "0.255,0.255,0.255";

            /// <summary>
            /// Sets Text Color for Container
            /// </summary>
            public static string TextColor_Container = "0,1,0";

            /// <summary>
            /// Sets Text Color for Building Privledge
            /// </summary>
            public static string TextColor_BuildingPrivledge = "1,0,1";

            /// <summary>
            /// Sets Text Color for NPC
            /// </summary>
            public static string TextColor_NPC = "0,1,1";

            /// <summary>
            /// Sets Text Color for Nodes
            /// </summary>
            public static string TextColor_Nodes = "0.255,0.255,0";

            #endregion
        }

        private new void LoadConfig()
        {
            GetConfig(ref Configuration.IconProfile, "General", "ChatIconSteamId");
            GetConfig(ref Configuration.ChatPrefix, "General", "ChatPrefix", "AdminRadar");
            GetConfig(ref Configuration.Command, "General", "Command");

            // Settings | Player
            GetConfig(ref Configuration.Filter_Player, "Settings", "Player", "Enabled");
            GetConfig(ref Configuration.RefreshRate_Player, "Settings", "Player", "RefreshRate");
            GetConfig(ref Configuration.ViewDistance_Player, "Settings", "Player", "ViewDistance");
            GetConfig(ref Configuration.BoxColor_Player, "Settings", "Player", "Colors", "Box");
            GetConfig(ref Configuration.TextColor_Player, "Settings", "Player", "Colors", "Text");

            // Settings | Sleeper
            GetConfig(ref Configuration.Filter_Sleeper, "Settings", "Sleeper", "Enabled");
            GetConfig(ref Configuration.RefreshRate_Sleeper, "Settings", "Sleeper", "RefreshRate");
            GetConfig(ref Configuration.ViewDistance_Sleeper, "Settings", "Sleeper", "ViewDistance");
            GetConfig(ref Configuration.TextColor_Sleeper, "Settings", "Sleeper", "Colors", "Text");

            // Settings | Container
            GetConfig(ref Configuration.Filter_Container, "Settings", "Container", "Enabled");
            GetConfig(ref Configuration.RefreshRate_Container, "Settings", "Container", "RefreshRate");
            GetConfig(ref Configuration.ViewDistance_Container, "Settings", "Container", "ViewDistance");
            GetConfig(ref Configuration.ArrowColor_Container, "Settings", "Container", "Colors", "Arrow");
            GetConfig(ref Configuration.TextColor_Container, "Settings", "Container", "Colors", "Text");

            // Settings | BuildingPrivledge
            GetConfig(ref Configuration.Filter_BuildingPrivledge, "Settings", "BuildingPrivledge", "Enabled");
            GetConfig(ref Configuration.RefreshRate_BuildingPrivledge, "Settings", "BuildingPrivledge", "RefreshRate");
            GetConfig(ref Configuration.ViewDistance_BuildingPrivledge, "Settings", "BuildingPrivledge", "ViewDistance");
            GetConfig(ref Configuration.ArrowColor_BuildingPrivledge, "Settings", "BuildingPrivledge", "Colors", "Arrow");
            GetConfig(ref Configuration.TextColor_BuildingPrivledge, "Settings", "BuildingPrivledge", "Colors", "Text");

            // Settings | NPC
            GetConfig(ref Configuration.Filter_NPC, "Settings", "NPC", "Enabled");
            GetConfig(ref Configuration.RefreshRate_NPC, "Settings", "NPC", "RefreshRate");
            GetConfig(ref Configuration.ViewDistance_NPC, "Settings", "NPC", "ViewDistance");
            GetConfig(ref Configuration.TextColor_NPC, "Settings", "NPC", "Colors", "Text");

            // Settings | Node
            GetConfig(ref Configuration.Filter_Node, "Settings", "Node", "Enabled");
            GetConfig(ref Configuration.RefreshRate_Node, "Settings", "Node", "RefreshRate");
            GetConfig(ref Configuration.ViewDistance_Node, "Settings", "Node", "ViewDistance");
            GetConfig(ref Configuration.TextColor_Nodes, "Settings", "Node", "Colors", "Text");
            GetConfig(ref Configuration.TextColor_Nodes, "Settings", "Node", "Colors", "Arrow");

            // Settings | MISC
            GetConfig(ref Configuration.ShowExtendedData, "Settings", "MISC", "ShowExtendedData");
            GetConfig(ref Configuration.RadarBoxes, "Settings", "MISC", "ShowBoxes");
            GetConfig(ref Configuration.RadarText, "Settings", "MISC", "ShowText");
            GetConfig(ref Configuration.RadarArrows, "Settings", "MISC", "ShowArrows");
            GetConfig(ref Configuration.DefaultSelectedFilter, "Settings", "MISC", "DefaultFilter");
            GetConfig(ref Configuration.Filter_All, "Settings", "MISC", "AllowAllFilter");

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file. . .");

        #endregion

        #region Localization

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["RadarOff"] = "Radar has been <color=red>DEACTIVATED</color>",
                ["NoAccess"] = "Unknown command: {0}",
                ["RadarActive"] = "Radar has been <color=green>ACTIVATED</color> | <color=aqua>Filter <color=green>{0}</color></color>",
                ["InvalidSyntax"] = "Invalid command syntax: /{0} help",
                ["CommandDisabled"] = "The Command /{0} {1} has been disabled by the server administrator",
                ["RadarGive"] = "Radar has been {0} for {1}",
                ["Enabled"] = "<color=green>Enabled</color>",
                ["Disabled"] = "<color=red>Disabled</color>",
                ["SettingUpdate"] = "Setting {0} {1} has been changed from {2} to {3}",
                ["RadarList"] = "------[ ActiveRadars ]------\n{0}",
                ["NoRadars"] = "No players are currently using radar"
            }, this, "en");
        }

        #endregion

        #region Commands

        [ChatCommand("radar")]
        void ccmdRadar(BasePlayer player, string command, string[] args)
        {
            if (!Allowed(player)) { player.ChatMessage(Lang("NoAccess", player.UserIDString, command)); return; }
            if (args.Length == 0) { ToggleRadar(player, RadarFilter.None); return; }

            switch (args[0].ToLower())
            {
                case "list":
                    if (args.Length > 1) { SendMessage(player, Lang("InvalidSyntax", player.UserIDString, command)); return; }
                    string activeplayers = string.Empty;
                    if (RadarList(out activeplayers))
                    {
                        player.ChatMessage(Lang("RadarList", player.UserIDString, activeplayers));
                        return;
                    }
                    SendMessage(player, Lang("NoRadars", player.UserIDString));
                    break;

                case "help":
                    SendHelpText(player);
                    break;

                case "filterlist":
                    if (args.Length > 1) { SendMessage(player, Lang("InvalidSyntax", player.UserIDString, command)); return; }
                    string msg = "<color=red>Filter List</color>\n";
                    foreach (var filter in FilterList)
                    {
                        msg += $"<color=green>{filter}</color>\n";
                    }
                    SendMessage(player, msg);
                    break;

                default:
                    var filtersel = filterValidation(string.Join(" ", args));
                    ToggleRadar(player, filtersel);
                    break;
            }
        }

        #endregion

        #region Plugin Methods

        void ToggleRadar(BasePlayer player, RadarFilter filter)
        {
            if (IsRadar(player.UserIDString))
            {
                if (ActiveRadars.Contains(player.UserIDString)) ActiveRadars.Remove(player.UserIDString);
                GameObject.Destroy(player.GetComponent<Radar>());
                if (filter == RadarFilter.None)
                {
                    SendMessage(player, Lang("RadarOff", player.UserIDString));
                    return;
                }
            }

            if (filter == RadarFilter.None) filter = filterValidation(Configuration.DefaultSelectedFilter);

            if (!ActiveRadars.Contains(player.UserIDString)) ActiveRadars.Add(player.UserIDString);
            Radar whrd = player.gameObject.AddComponent<Radar>();

            whrd.filter = filter;
            whrd.InitRadar();
            SendMessage(player, Lang("RadarActive", player.UserIDString, filter.ToString().ToUpper()));
        }

        #endregion

        #region Clamping & Value Validation

        RadarFilter filterValidation(string filter)
        {
            RadarFilter filters = 0;
            if (Configuration.Filter_Player && Regex.IsMatch(filter, @"(pla|hack|ply)", RegexOptions.IgnoreCase)) filters |= RadarFilter.Player;
            if (Configuration.Filter_Sleeper && Regex.IsMatch(filter, @"(sle)", RegexOptions.IgnoreCase)) filters |= RadarFilter.Sleeper;
            if (Configuration.Filter_Container && Regex.IsMatch(filter, @"(sto|con|box)", RegexOptions.IgnoreCase)) filters |= RadarFilter.Container;
            if (Configuration.Filter_BuildingPrivledge && Regex.IsMatch(filter, @"(tool|cup|cab|tc|auth|priv)", RegexOptions.IgnoreCase)) filters |= RadarFilter.ToolCupboard;
            if (Configuration.Filter_NPC && Regex.IsMatch(filter, @"(ani|npc|bear|stag|deer|boar|chic)", RegexOptions.IgnoreCase)) filters |= RadarFilter.NPC;
            if (Configuration.Filter_Node && Regex.IsMatch(filter, @"(nod|col|roc|tre|res)", RegexOptions.IgnoreCase)) filters |= RadarFilter.ResourceNode;
            if (Configuration.Filter_All && filter.ToLower() == "all") filters = RadarFilter.All;
            return filters;
        }

        void LoadFilterList()
        {
            FilterList.Clear();
            if (Configuration.Filter_Player) FilterList.Add("player");
            if (Configuration.Filter_Sleeper) FilterList.Add("sleeper");
            if (Configuration.Filter_Container) FilterList.Add("storage");
            if (Configuration.Filter_BuildingPrivledge) FilterList.Add("toolcupboard");
            if (Configuration.Filter_NPC) FilterList.Add("npc");
            if (Configuration.Filter_Node) FilterList.Add("node");
            if (Configuration.Filter_All) FilterList.Add("all");
        }

        #endregion

        #region Helper

        void SendMessage(BasePlayer player, string message) => rust.SendChatMessage(player, $"<color=grey>[<color=teal>{Configuration.ChatPrefix ?? Title}</color>]</color>", "<color=grey>" + message + "</color>", Configuration.IconProfile ?? "0");

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        bool IsRadar(string id) => ActiveRadars.Contains(id);

        bool Allowed(BasePlayer player) => ServerUsers.Is(player.userID, ServerUsers.UserGroup.Owner) || ServerUsers.Is(player.userID, ServerUsers.UserGroup.Moderator);

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        bool RadarList(out string list)
        {
            string namelist = string.Empty;
            foreach (var key in ActiveRadars)
            {
                namelist += $"<color=red>{rust.FindPlayer(key).displayName}</color>\n";
            }
            list = namelist;
            return ActiveRadars.Count != 0;
        }

        private void SendHelpText(BasePlayer player)
        {
            string message =
                "<size=13>---- Radar Commands ----\n" +
                "<color=red>/radar</color> <color=green>(filter)</color> - <color=yellow>activates radar with default settings or with optional filter</color>\n" +
                "<color=red>/radar list</color> - <color=yellow>Shows a list of players using Radar</color>\n" +
                "<color=red>/radar filterlist</color> - <color=yellow>shows available filters</color>";

            if (Allowed(player))
            {
                player.ChatMessage(message);
            }
        }

        public static bool HasFlag(RadarFilter instance, RadarFilter selected)
        {
            return ((instance & selected) == selected);
        }

        #endregion
    }
}