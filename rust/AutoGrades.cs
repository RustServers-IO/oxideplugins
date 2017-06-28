/*
	Created By AlexALX (c) 2015-2017
	Special thanks to: recon, freaky
	for keep plugin work with latest rust updates
*/
using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Automatic Build Grades", "AlexALX", "0.0.18", ResourceId = 921)]
    [Description("Auto update grade on build to what you need")]
    public class AutoGrades : CovalencePlugin
    {
        #region Initialization

        private Dictionary<string, Timer> CheckTimer = new Dictionary<string, Timer>();
        private Dictionary<string, PlayerGrades> playerGrades;
        private bool LoadDefault = false;
        private bool block = true;
        private string cmdname = "bgrade";
        private bool allow_timer = true;
        private int def_timer = 30;

        private class PlayerGrades
        {
            public int Grade { get; set; }
            public int Timer { get; set; }

            public PlayerGrades(int grade = 0, int timer = 0)
            {
                Grade = grade;
                Timer = timer;
            }
        }

        protected override void LoadDefaultConfig()
        {
            LoadDefault = true;
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission("autogrades.all", this);
            permission.RegisterPermission("autogrades.1", this);
            permission.RegisterPermission("autogrades.2", this);
            permission.RegisterPermission("autogrades.3", this);
            permission.RegisterPermission("autogrades.4", this);
            permission.RegisterPermission("autogrades.nores", this);

            ReadFromConfig("Block Construct and Refund", ref block);
            ReadFromConfig("Command", ref cmdname);
            ReadFromConfig("AllowTimer", ref allow_timer);
            ReadFromConfig("DefaultTimer", ref def_timer);
            SaveConfig();

            AddCovalenceCommand(cmdname, "BuildGradeCommand");

            playerGrades = new Dictionary<string, PlayerGrades>();
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BGRADE_NOPERM"] = "You have no access to this command.",
                ["BGRADE_NORES"] = "Not enough resources for construct and upgrade.",
                ["BGRADE_NORES2"] = "Not enough resources for upgrade.",
                ["BGRADE_HELP"] = "Automatic Build Grade command usage:",
                ["BGRADE_1"] = "/{0} 1 - auto update to wood",
                ["BGRADE_2"] = "/{0} 2 - auto update to stone",
                ["BGRADE_3"] = "/{0} 3 - auto update to metal",
                ["BGRADE_4"] = "/{0} 4 - auto update to armored",
                ["BGRADE_0"] = "/{0} 0 - disable auto update",
                ["BGRADE_CUR"] = "Current mode: {0}",
                ["BGRADE_SET"] = "You successfully set auto update to {0}.",
                ["BGRADE_DIS"] = "You successfully disabled auto update.",
                ["BGRADE_INV"] = "Invalid building grade.",
                ["Disabled"] = "Disabled",
                ["Wood"] = "Wood",
                ["Stone"] = "Stone",
                ["Metal"] = "Metal",
                ["TopTier"] = "TopTier",
                ["BGRADE_T"] = "/{0} t sec - timer to auto turn off, use 0 to disable",
                ["BGRADE_CURT"] = ", timer: {0}",
                ["BGRADE_TIME"] = "Auto turn off timer: {0}.",
                ["BGRADE_SET_TIMER"] = "You successfully set auto turn off timer to {0}.",
                ["BGRADE_DIS_TIMER"] = "You successfully disabled auto turn off timer.",
                ["BGRADE_DIS_TIMED"] = "Auto update automatically disabled.",
                ["BGRADE_SEC"] = "seconds",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BGRADE_NOPERM"] = "Недостаточно прав для использования данной команды.",
                ["BGRADE_NORES"] = "Недостаточно ресурсов для постройки и обновления.",
                ["BGRADE_NORES2"] = "Недостаточно ресурсов для обновления.",
                ["BGRADE_HELP"] = "Автоматическое обновление конструкции, использование:",
                ["BGRADE_1"] = "/{0} 1 - авто обновление до дерева",
                ["BGRADE_2"] = "/{0} 2 - авто обновление до камня",
                ["BGRADE_3"] = "/{0} 3 - авто обновление до метала",
                ["BGRADE_4"] = "/{0} 4 - авто обновление до бронированого",
                ["BGRADE_0"] = "/{0} 0 - отключить авто обновление",
                ["BGRADE_CUR"] = "Текущий режим: {0}",
                ["BGRADE_SET"] = "Вы успешно установили авто обновление до: {0}.",
                ["BGRADE_DIS"] = "Вы успешно выключили авто обновление.",
                ["BGRADE_INV"] = "Неверный класс постройки.",
                ["Disabled"] = "Отключено",
                ["Wood"] = "Дерево",
                ["Stone"] = "Камень",
                ["Metal"] = "Метал",
                ["TopTier"] = "Бронированый",
                ["BGRADE_T"] = "/{0} t сек - таймер до авто-отключения, 0 - отключить",
                ["BGRADE_CURT"] = ", таймер: {0}",
                ["BGRADE_TIME"] = "Время до авто отключения: {0}.",
                ["BGRADE_SET_TIMER"] = "Вы успешно установили таймер авто отключения на {0}.",
                ["BGRADE_DIS_TIMER"] = "Вы успешно выключили таймер авто отключения.",
                ["BGRADE_DIS_TIMED"] = "Авто обновление автоматически отключено.",
                ["BGRADE_SEC"] = "секунд",
            }, this, "ru");
        }

        #endregion

        #region Helpers

        private void ReadFromConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null) var = (T)Convert.ChangeType(Config[Key], typeof(T));
            Config[Key] = var;
        }

        private bool HasPerm(string steamId, string perm) => permission.UserHasPermission(steamId, "autogrades." + perm);

        private bool HasAnyPerm(BasePlayer player)
        {
            var steamId = player.UserIDString;
            return (HasPerm(steamId, "all") || HasPerm(steamId, "1") || HasPerm(steamId, "2") || HasPerm(steamId, "3") || HasPerm(steamId, "4"));
        }

        private string GetMessage(string name, string steamId = null) => lang.GetMessage(name, this, steamId);

        #endregion

        private int PlayerTimer(string steamId, bool cache = true)
        {
            if (!allow_timer) return 0;
            if (playerGrades.ContainsKey(steamId)) return playerGrades[steamId].Timer;
            if (!cache) return def_timer;
            playerGrades[steamId] = new PlayerGrades(0, def_timer);
            return playerGrades[steamId].Timer;
        }

        private void UpdateTimer(BasePlayer player)
        {
            var steamId = player.UserIDString;
            var ptimer = PlayerTimer(steamId, false);

            if (ptimer > 0)
            {
                if (CheckTimer.ContainsKey(steamId))
                {
                    CheckTimer[steamId].Destroy();
                    CheckTimer.Remove(steamId);
                }
                CheckTimer[steamId] = timer.Once(ptimer, () =>
                {
                    if (CheckTimer.ContainsKey(steamId)) CheckTimer.Remove(steamId);
                    if (playerGrades.ContainsKey(steamId)) playerGrades[steamId].Grade = 0;
                    player?.ChatMessage(GetMessage("BGRADE_DIS_TIMED", steamId));
                });
            }
        }

        private int PlayerGrade(string steamId, bool cache = true)
        {
            if (playerGrades.ContainsKey(steamId)) return playerGrades[steamId].Grade;
            if (!cache) return 0;
            playerGrades[steamId] = new PlayerGrades(0, def_timer);
            return playerGrades[steamId].Grade;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            var steamId = player.UserIDString;
            if (CheckTimer.ContainsKey(steamId))
            {
                CheckTimer[steamId].Destroy();
                CheckTimer.Remove(steamId);
            }
            if (playerGrades.ContainsKey(steamId)) playerGrades.Remove(steamId);
        }

        private bool CanAffordToUpgrade(BasePlayer player, int grade, BuildingBlock buildingBlock)
        {
            var flag = true;
            var enumerator = buildingBlock.blockDefinition.grades[(int)buildingBlock.grade].costToBuild.GetEnumerator();

            // Add cost of build grade 0
            var costs = new Dictionary<int, float>();
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                costs[current.itemid] = current.amount;
            }

            enumerator = buildingBlock.blockDefinition.grades[grade].costToBuild.GetEnumerator();

            // Calculate needed costs for upgrade
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                var cost = 0f;
                if (costs.ContainsKey(current.itemid))
                {
                    cost = costs[current.itemid];
                    costs.Remove(current.itemid);
                }
                if (player.inventory.GetAmount(current.itemid) >= current.amount + cost) continue;
                flag = false;
                return flag;
            }

            // Check for build grade 0 and needed cost (additional resources)
            if (costs.Count > 0)
            {
                foreach (var kvp in costs)
                {
                    if (player.inventory.GetAmount(kvp.Key) >= kvp.Value) continue;
                    flag = false;
                    return flag;
                }
            }

            return flag;
        }

        /* Example of hook usage
        private int CanAutoGrade(BasePlayer player, int grade, BuildingBlock buildingBlock, Planner planner)
        {
            //return -1; // Block upgrade, but create twig part
            //return 0; // Obey plugin settings (block on construct if enabled or not)
            //return 1; // Block upgrade and block build
            return; // allow upgrade
        }*/

        private void OnEntityBuilt(Planner planner, UnityEngine.GameObject gameObject)
        {
            var player = planner.GetOwnerPlayer();
            if (player != null && !player.CanBuild() || !HasAnyPerm(player)) return;

            var buildingBlock = gameObject.GetComponent<BuildingBlock>();
            if (buildingBlock == null) return;

            var steamId = player.UserIDString;
            var pgrade = PlayerGrade(steamId, false);
            var buildingGrade = (int)buildingBlock.grade;

            if (pgrade > 0)
            {
                if (!HasPerm(steamId, "all") && !HasPerm(steamId, pgrade.ToString())) return;

                var result = Interface.CallHook("CanAutoGrade", player, pgrade, buildingBlock, planner);
                if (result is int)
                {
                    if ((int)result == 0 && !block || (int)result < 0) return;
                    if (buildingBlock.blockDefinition.grades[buildingGrade])
                    {
                        var items = buildingBlock.blockDefinition.grades[buildingGrade].costToBuild;
                        foreach (var itemAmount in items)
                        {
                            player.Command("note.inv", itemAmount.itemid, (int)itemAmount.amount);
                            player.inventory.GiveItem(ItemManager.CreateByItemID(itemAmount.itemid, (int)itemAmount.amount), player.inventory.containerMain);
                        }
                    }
                    gameObject.GetComponent<BaseEntity>().KillMessage();
                    return;
                }

                if (!HasPerm(steamId, "nores"))
                {
                    var amount = 0;
                    if (pgrade > buildingGrade && buildingBlock.blockDefinition.grades[pgrade])
                    {
                        var items = buildingBlock.blockDefinition.grades[buildingGrade].costToBuild;
                        if (!CanAffordToUpgrade(player, pgrade, buildingBlock))
                        {
                            if (!block)
                            {
                                player.ChatMessage(GetMessage("BGRADE_NORES2", steamId));
                                return;
                            }

                            foreach (var itemAmount in items)
                            {
                                player.Command("note.inv", itemAmount.itemid, (int)itemAmount.amount);
                                player.inventory.GiveItem(ItemManager.CreateByItemID(itemAmount.itemid, (int)itemAmount.amount), player.inventory.containerMain);
                            }
                            gameObject.GetComponent<BaseEntity>().KillMessage();
                            player.ChatMessage(GetMessage("BGRADE_NORES", steamId));
                        }
                        else
                        {
                            buildingBlock.SetGrade((BuildingGrade.Enum)pgrade);
                            buildingBlock.UpdateSkin();
                            buildingBlock.SetHealthToMax();
                            buildingBlock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            Interface.CallHook("OnStructureUpgrade", buildingBlock, player, (BuildingGrade.Enum)pgrade);
                            UpdateTimer(player);

                            var items2 = buildingBlock.blockDefinition.grades[pgrade].costToBuild;
                            var items3 = new List<Item>();

                            foreach (var itemAmount in items2)
                            {
                                amount = (int)Math.Ceiling(itemAmount.amount);
                                player.inventory.Take(items3, itemAmount.itemid, amount);
                                player.Command("note.inv", itemAmount.itemid, amount * -1f);
                            }
                        }
                    }
                }
                else
                {
                    buildingBlock.SetGrade((BuildingGrade.Enum)pgrade);
                    buildingBlock.UpdateSkin();
                    buildingBlock.SetHealthToMax();
                    buildingBlock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    UpdateTimer(player);
                }
            }
        }

        #region Commands

        private void BuildGradeCommand(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                player.Reply($"Command '{command}' can only be used by players", command);
                return;
            }

            if (!basePlayer.CanBuild() || !HasAnyPerm(basePlayer))
            {
                player.Reply(GetMessage("BGRADE_NOPERM", player.Id));
                return;
            }

            var chatmsg = new List<string>();
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "1":
                    case "2":
                    case "3":
                    case "4":
                        if (!HasPerm(player.Id, "all") && !HasPerm(player.Id, args[0]))
                        {
                            player.Reply(GetMessage("BGRADE_NOPERM", player.Id));
                            return;
                        }

                        var pgrade = PlayerGrade(player.Id);
                        playerGrades[player.Id].Grade = Convert.ToInt32(args[0]);
                        chatmsg.Add(string.Format(GetMessage("BGRADE_SET", player.Id), GetMessage(((BuildingGrade.Enum)playerGrades[player.Id].Grade).ToString(), player.Id)));
                        if (allow_timer)
                        {
                            var ptimer = PlayerTimer(player.Id);
                            if (ptimer > 0)
                            {
                                chatmsg.Add(string.Format(GetMessage("BGRADE_TIME", player.Id), ptimer + " " + GetMessage("BGRADE_SEC", player.Id)));
                                UpdateTimer(basePlayer);
                            }
                        }
                        break;

                    case "0":
                        playerGrades.Remove(player.Id);
                        chatmsg.Add(GetMessage("BGRADE_DIS", player.Id));
                        break;

                    case "t":
                        if (args.Length > 1 && allow_timer)
                        {
                            var vtimer = Convert.ToInt32(args[1]);
                            if (vtimer < 0) vtimer = 0;
                            var ptimer = PlayerGrade(player.Id);
                            playerGrades[player.Id].Timer = vtimer;
                            if (vtimer > 0)
                                chatmsg.Add(string.Format(GetMessage("BGRADE_SET_TIMER", player.Id), vtimer + " " + GetMessage("BGRADE_SEC", player.Id)));
                            else
                                chatmsg.Add(GetMessage("BGRADE_DIS_TIMER", player.Id));
                        }
                        else
                            chatmsg.Add(GetMessage("BGRADE_INV", player.Id));
                        break;

                    default:
                        chatmsg.Add(GetMessage("BGRADE_INV", player.Id));
                        break;
                }
            }
            else
            {
                var pgrade = PlayerGrade(player.Id, false);
                chatmsg.Add(GetMessage("BGRADE_HELP", player.Id) + "\n");
                var all = HasPerm(player.Id, "all");
                if (all || HasPerm(player.Id, "1")) chatmsg.Add(string.Format(GetMessage("BGRADE_1", player.Id), cmdname));
                if (all || HasPerm(player.Id, "2")) chatmsg.Add(string.Format(GetMessage("BGRADE_2", player.Id), cmdname));
                if (all || HasPerm(player.Id, "3")) chatmsg.Add(string.Format(GetMessage("BGRADE_3", player.Id), cmdname));
                if (all || HasPerm(player.Id, "4")) chatmsg.Add(string.Format(GetMessage("BGRADE_4", player.Id), cmdname));
                chatmsg.Add(string.Format(GetMessage("BGRADE_0", player.Id), cmdname));
                if (allow_timer) chatmsg.Add(string.Format(GetMessage("BGRADE_T", player.Id), cmdname));
                var curtxt = ((BuildingGrade.Enum)pgrade).ToString();
                if (pgrade == 0) curtxt = "Disabled";
                var msg = string.Format(GetMessage("BGRADE_CUR", player.Id), GetMessage(curtxt, player.Id));
                if (allow_timer)
                {
                    var ptimer = PlayerTimer(player.Id, false);
                    curtxt = (ptimer > 0 ? ptimer + " " + GetMessage("BGRADE_SEC", player.Id) : "Disabled");
                    msg += string.Format(GetMessage("BGRADE_CURT", player.Id), GetMessage(curtxt, player.Id));
                }
                chatmsg.Add("\n" + msg);
            }
            player.Reply(string.Join("\n", chatmsg.ToArray()));
        }

        #endregion
    }
}
