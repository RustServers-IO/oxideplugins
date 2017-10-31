using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PlayerReport", "hoppel", "1.0.7", ResourceId = 2635)]
    class PlayerReport : RustPlugin
    {
        #region Fields

        [PluginReference]
        Plugin DiscordMessages;

        private List<string> openUI = new List<string>();
        private HashSet<ulong> cooldowns = new HashSet<ulong>();
        private const string Permname = "playerreport.use";
        private const string Permnameblock = "playerreport.block";

        #endregion

        #region Oxide Hooks

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var victim = entity?.ToPlayer();
            var killer = info?.Initiator?.ToPlayer();
            if (victim == null || killer == null)
                return;

            if (killer == victim)
                return;

            if (!permission.UserHasPermission(victim.UserIDString, Permname) || permission.UserHasPermission(victim.UserIDString, Permnameblock))
                return;

            var killername = killer.displayName.Replace(" ", "_");

            var weaponName = "Unknown";
            if (info.Weapon != null)
            {
                var usedItem = info.Weapon.GetItem();
                if (usedItem != null)
                    weaponName = usedItem.info.displayName.english.Replace(" ", "_");
            }

            var distance = Mathf.Round(info.ProjectileDistance).ToString();
            if (string.IsNullOrEmpty(distance) || cooldowns.Contains(victim.userID))
                return;

            DeathReportUI(victim, killer.UserIDString, distance, killername, weaponName);
            openUI.Add(victim.UserIDString);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
                return;

            if (openUI.Contains(player.UserIDString))
                DestroyUI(player);
        }

        private void Loaded()
        {
            storedData = new StoredData();
        }

        private void Unload()
        {
            storedData.Save();

            foreach (var entry in openUI)
            {
                var player = BasePlayer.Find(entry);
                if (player == null)
                    continue;

                DestroyUI(player);
            }
        }

        #endregion

        private void Cooldownhandling(BasePlayer player)
        {
            if (cooldowns.Contains(player.userID))
                return;

            cooldowns.Add(player.userID);
            timer.Once(Cooldown, () => cooldowns.Remove(player.userID));
        }

        #region Commands

        [ConsoleCommand("ReportHacking")]
        private void cmdReportHacking(ConsoleSystem.Arg arg)
        {
            var killer = arg.Args[0];
            var distance = arg.Args[1];
            var killername = arg.Args[2].Replace("_", " ");
            var weapon = arg.Args[3].Replace("_", " ");
            var player = arg.Player();
            if (player == null)
                return;
            if (!storedData.PlayerInformation.ContainsKey(player.UserIDString))
            {
                storedData.PlayerInformation.Add(player.UserIDString, new ReportInfo() { Reported = 0, Reports = 0 });
            }
            if (!storedData.PlayerInformation.ContainsKey(killer))
            {
                storedData.PlayerInformation.Add(killer, new ReportInfo() { Reported = 0, Reports = 0 });
            }
            storedData.PlayerInformation[killer].Reported++;
            storedData.PlayerInformation[player.UserIDString].Reports++;
            storedData.Save();
            if (storedData.PlayerInformation[killer].Reported < requiredReports)
            {
                DestroyUI(player);
                return;
            }
            Cooldownhandling(player);
            if (enableTickets)
            {
                player.SendConsoleCommand("ticket create " + msg("Button1Re", null, killer, distance, killername));
            }
            if (enableDiscordMessages)
            {
                DiscordMessage(player.displayName, player.UserIDString, msg("DiscordButton1Field2"), killername, killer, distance, weapon, storedData.PlayerInformation[killer].Reported, storedData.PlayerInformation[killer].Reports);
            }
            if (openUI.Contains(player.UserIDString))
            {
                DestroyUI(player);
            }
        }

        [ConsoleCommand("ReportGroupLimit")]
        private void cmdReportGroupLimit(ConsoleSystem.Arg arg)
        {
            var killer = arg.Args[0];
            var distance = arg.Args[1];
            var killername = arg.Args[2].Replace("_", " ");
            var weapon = arg.Args[3].Replace("_", " ");
            var player = arg.Player();
            if (player == null)
                return;
            if (!storedData.PlayerInformation.ContainsKey(player.UserIDString))
            {
                storedData.PlayerInformation.Add(player.UserIDString, new ReportInfo() { Reported = 0, Reports = 0 });
            }
            if (!storedData.PlayerInformation.ContainsKey(killer))
            {
                storedData.PlayerInformation.Add(killer, new ReportInfo() { Reported = 0, Reports = 0 });
            }
            storedData.PlayerInformation[killer].Reported++;
            storedData.PlayerInformation[player.UserIDString].Reports++;
            storedData.Save();
            if (storedData.PlayerInformation[killer].Reported < requiredReports)
            {
                DestroyUI(player);
                return;
            }
            Cooldownhandling(player);
            if (enableTickets)
                player.SendConsoleCommand("ticket create " + msg("Button2Re", null, killer, distance, killername));
            if (enableDiscordMessages)
                DiscordMessage(player.displayName, player.UserIDString, msg("DiscordButton2Field2"), killername, killer, distance, weapon, storedData.PlayerInformation[killer].Reported, storedData.PlayerInformation[killer].Reports);
            if (openUI.Contains(player.UserIDString))
                DestroyUI(player);
        }

        [ConsoleCommand("ReportBugAbuse")]
        private void cmdReportBugAbuse(ConsoleSystem.Arg arg)
        {
            var killer = arg.Args[0];
            var distance = arg.Args[1];
            var killername = arg.Args[2].Replace("_", " ");
            var weapon = arg.Args[3].Replace("_", " ");
            var player = arg.Player();
            if (player == null)
                return;
            if (!storedData.PlayerInformation.ContainsKey(player.UserIDString))
            {
                storedData.PlayerInformation.Add(player.UserIDString, new ReportInfo() { Reported = 0, Reports = 0 });
            }
            if (!storedData.PlayerInformation.ContainsKey(killer))
            {
                storedData.PlayerInformation.Add(killer, new ReportInfo() { Reported = 0, Reports = 0 });
            }
            storedData.PlayerInformation[killer].Reported++;
            storedData.PlayerInformation[player.UserIDString].Reports++;
            storedData.Save();
            if (storedData.PlayerInformation[killer].Reported < requiredReports)
            {
                DestroyUI(player);
                return;
            }
            Cooldownhandling(player);
            if (enableTickets)
                player.SendConsoleCommand("ticket create " + msg("Button3Re", null, killer, distance, killername));
            if (enableDiscordMessages)
                DiscordMessage(player.displayName, player.UserIDString, msg("DiscordButton3Field2"), killername, killer, distance, weapon, storedData.PlayerInformation[killer].Reported, storedData.PlayerInformation[killer].Reports);
            if (openUI.Contains(player.UserIDString))
                DestroyUI(player);
        }

        private void DiscordMessage(string displayName, string userId, string reportFunction, string killerName,
            string killerUserId, string distance, string weapon, int reported, int reports)
        {
            var fields = new List<Fields>();
            fields.Add(new Fields(msg("DiscordButton1FieldName1"), $"[{displayName}](https://steamcommunity.com/profiles/{userId}) ({reports})", true));
            fields.Add(new Fields(msg("DiscordButton1FieldName2"), msg(reportFunction), true));
            fields.Add(new Fields(msg("DiscordButton1FieldName3"), $"[{killerName}](https://steamcommunity.com/profiles/{killerUserId})", true));
            fields.Add(new Fields(msg("DiscordButton1FieldName6"), $"{reported}", true));
            fields.Add(new Fields(msg("DiscordButton1FieldName4"), $"{distance}m", true));
            fields.Add(new Fields(msg("DiscordButton1FieldName5"), $"{weapon}", true));
            var serializedObject = JsonConvert.SerializeObject(fields);
            DiscordMessages?.Call("API_SendFancyMessage", webhookURL, Servername, 0, serializedObject);
            if (Alert) DiscordMessages?.Call("API_SendTextMessage", webhookURL, "@here");
        }

        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }

        #endregion

        #region UI

        void DeathReportUI(BasePlayer player, string killer, string distance, string killername, string weaponName)
        {
            var container = new CuiElementContainer();
            var ReportUI = container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.0",
                },
                RectTransform =
                {
                    AnchorMin = "0.25 0.861",
                    AnchorMax = "0.771 1"
                },
                CursorEnabled = false
            }, "Overlay", "ReportUI");
            if (Button1Enabled)
            {
                var Button1 = container.Add(new CuiButton
                {
                    Button =
                {
                Command = $"global.ReportHacking {killer} {distance} {killername} {weaponName}",
                Color = Button1Color
                },
                    RectTransform =
                {
                AnchorMin = Button1AnchMin,
                AnchorMax = Button1AnchMax
                },
                    Text =
                {
                Text = msg("Button1", null),
                    FontSize = 15,
                    Align = TextAnchor.MiddleCenter
                }
                }, ReportUI);
            }
            if (Button2Enabled)
            {
                var Button2 = container.Add(new CuiButton
                {
                    Button =
                {
                Command = $"global.ReportGroupLimit {killer} {distance} {killername} {weaponName}",
                Color = Button2Color
                },
                    RectTransform =
                {
                AnchorMin = Button2AnchMin,
                    AnchorMax = Button2AnchMax
                },
                    Text =
                {
                Text = msg("Button2", null),
                    FontSize = 15,
                    Align = TextAnchor.MiddleCenter
                }
                }, ReportUI);
            }
            if (Button3Enabled)
            {
                var Button3 = container.Add(new CuiButton
                {
                    Button =
                {
                Command = $"global.ReportBugAbuse {killer} {distance} {killername} {weaponName}",
                Color = Button3Color
                },
                    RectTransform =
                {
                AnchorMin = Button3AnchMin,
                    AnchorMax = Button3AnchMax
                },
                    Text =
                {
                Text = msg("Button3", null),
                    FontSize = 15,
                    Align = TextAnchor.MiddleCenter
                }
                }, ReportUI);
            }
           var Text = container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0 0.028",
                    AnchorMax = "1 0.290"
                },
                CursorEnabled = false
            }, ReportUI);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = msg("InfoMessage", null),
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, Text);
            CuiHelper.AddUi(player, container);
        }

        void DestroyUI(BasePlayer player)
        {
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, "ReportUI");
            CuiHelper.DestroyUi(player, "Report_Info");
            openUI.Remove(player.UserIDString);
        }

        #endregion

        #region Config
        string _color = "255 255 255 1";
        int requiredReports = 1;
        string Servername = "Servername";
        float Cooldown = 50;
        string webhookURL = "DISCORD WEBHOOK URL";
        bool Alert = true;
        bool enableTickets = true;
        bool enableDiscordMessages = true;
        bool Button1Enabled = true;
        string Button1Color = "0.41 0.5 0.25 1";
        string Button1AnchMin = "0 0.283";
        string Button1AnchMax = "0.25 0.75";
        bool Button2Enabled = true;
        string Button2Color = "0.12 0.38 0.57 1";
        string Button2AnchMin = "0.375 0.283";
        string Button2AnchMax = "0.625 0.75";
        bool Button3Enabled = true;
        string Button3Color = "0.57 0.21 0.11 1";
        string Button3AnchMin = "0.75 0.283";
        string Button3AnchMax = "1 0.75";

        new void LoadConfig()
        {
            GetConfig(ref requiredReports, "Settings", "Reports needed to send a Report");
            GetConfig(ref Alert, "Settings", "Enable @here Message in Discord");
            GetConfig(ref Servername, "Settings", "Your servername (for discord messages)");
            GetConfig(ref enableTickets, "Settings", "Enable Ticket Reports");
            GetConfig(ref Cooldown, "Settings", "GUI Cooldown");
            GetConfig(ref webhookURL, "Settings", "Discord Webhook URL");
            GetConfig(ref enableDiscordMessages, "Settings", "Enable DiscordMessages");
            GetConfig(ref Button1Enabled, "Button 1", "Button 1 Enabled");
            GetConfig(ref Button1Color, "Button 1", "Button 1 Color");
            GetConfig(ref Button1AnchMin, "Button 1", "Button 1 AnchorMin");
            GetConfig(ref Button1AnchMax, "Button 1", "Button 1 AnchorMax");
            GetConfig(ref Button2Enabled, "Button 2", "Button 2 Enabled");
            GetConfig(ref Button2Color, "Button 2", "Button 2 Color");
            GetConfig(ref Button2AnchMin, "Button 2", "Button 2 AnchorMin");
            GetConfig(ref Button2AnchMax, "Button 2", "Button 2 AnchorMax");
            GetConfig(ref Button3Enabled, "Button 3", "Button 3 Enabled");
            GetConfig(ref Button3Color, "Button 3", "Button 3 Color");
            GetConfig(ref Button3AnchMin, "Button 3", "Button 3 AnchorMin");
            GetConfig(ref Button3AnchMax, "Button 3", "Button 3 AnchorMax");
            SaveConfig();
        }

        void Init()
        {
            LoadConfig();
            permission.RegisterPermission(Permname, this);
            permission.RegisterPermission(Permnameblock, this);
        }

        void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");
        #endregion

        #region Lang

        public string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        public string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InfoMessage"] = "Abusing this system will result in a ban!",
                ["Button1"] = "Report: Hacking ",
                ["Button2"] = "Report: Group Limit ",
                ["Button3"] = "Report: Bug Abusing ",
                ["Button1Re"] = "reported {2} ({0}) for hacking distance {1}m",
                ["Button2Re"] = "reported {2} ({0}) for exceeding the Group Limit",
                ["Button3Re"] = "reported {2} ({0}) for Bug Abusing distance {1}m",
                ["DiscordButton1FieldName1"] = "Player",
                ["DiscordButton1FieldName2"] = "Reason",
                ["DiscordButton1FieldName3"] = "Reported",
                ["DiscordButton1FieldName4"] = "Distance",
                ["DiscordButton1FieldName5"] = "Weapon",
                ["DiscordButton1FieldName6"] = "Reported",
                ["DiscordButton1FieldName7"] = "Reporter Reports",
                ["DiscordButton1Field2"] = "Hacking",
                ["DiscordButton2FieldName1"] = "Player",
                ["DiscordButton2FieldName2"] = "Reason",
                ["DiscordButton2FieldName3"] = "Reported",
                ["DiscordButton2FieldName4"] = "Distance",
                ["DiscordButton2FieldName5"] = "Weapon",
                ["DiscordButton2FieldName6"] = "Reported",
                ["DiscordButton2FieldName7"] = "Reporter Reports",
                ["DiscordButton2Field2"] = "Group Limit",
                ["DiscordButton3FieldName1"] = "Player",
                ["DiscordButton3FieldName2"] = "Reason",
                ["DiscordButton3FieldName3"] = "Reported",
                ["DiscordButton3FieldName4"] = "Distance",
                ["DiscordButton3FieldName5"] = "Weapon",
                ["DiscordButton3FieldName6"] = "Reported",
                ["DiscordButton3FieldName7"] = "Reporter Reports",
                ["DiscordButton3Field2"] = "Bug Abuse",
            }, this);
        }

        #endregion

        #region Data

        private StoredData storedData;

        private class StoredData
        {
            public Dictionary<string, ReportInfo> PlayerInformation = new Dictionary<string, ReportInfo>();

            public StoredData()
            {
                Read();
            }

            public void Read() => PlayerInformation = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ReportInfo>>("PlayerReport");

            public void Save() => Interface.Oxide.DataFileSystem.WriteObject("PlayerReport", PlayerInformation);
        }

        private class ReportInfo
        {
            public int Reports;
            public int Reported;
        
            public ReportInfo()
            {
            }
        }

        #endregion
    }
}
