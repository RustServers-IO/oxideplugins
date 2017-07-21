using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("RunningMan", "sami37 - Мизантроп", "1.3.0")]
    [Description("Running Man is a short plugin where you have to kill the runner.")]
    class RunningMan : RustPlugin
    {
        private Timer stillRunnerTimer;
        private Command command = Interface.Oxide.GetLibrary<Command>();
        private Dictionary<string, int> SavedReward = new Dictionary<string, int>();
        private BasePlayer runningman;
        private Timer eventstart;
        private Timer eventpause;
        private double time1;
        private double time2;
        Random rnd = new Random();

        [PluginReference]
        Plugin Economics;
        
        [PluginReference]
        Plugin KarmaSystem;
        
        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());

        void Loaded()
        {
            LoadDefaultConfig();
            command.AddChatCommand("eventon", this, "cmdEvent");
            command.AddChatCommand("eventoff", this, "cmdEventOff");
            command.AddChatCommand("run", this, "cmdRun");
            command.AddConsoleCommand("eventon", this, "ccmdEvent");
            command.AddConsoleCommand("eventoff", this, "cmdEventOf");
            if (!Economics)
            {
                Puts("Economics not found!");
            }
            if ((string) Config["Default", "On"] == "true")
            {
                eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            LoadSavedData();
        }

        protected override void LoadDefaultConfig()
        {
            SetConfig("Default", "ChatName", "EVENT");
            SetConfig("Default", "authLevel", 1);
            SetConfig("Default", "On", "true");
            SetConfig("Default", "Count", 2);
            SetConfig("Default", "StarteventTime", 30);
            SetConfig("Default", "PauseeventTime", 30);
            SetConfig("Default", "DisconnectPendingTimer", 30);
            SetConfig("Config", "Reward", "Random", "true");
            SetConfig("Config", "Reward", "RewardFixing", "wood");
            SetConfig("Config", "Reward", "RewardFixingAmount", 10000);
            SetConfig("Config", "Reward", "KarmaSystem", "PointToRemove", 0);
            SetConfig("Config", "Reward", "KarmaSystem", "PointToAdd", 1);
            SaveConfig();
        }

        void LoadSavedData()
        {
            try
            {
                SavedReward = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>(nameof(RunningMan));
            }
            catch (Exception)
            {
                SavedReward = new Dictionary<string, int>
                {
                    {"Karma", 1},
                    {"wood", 10000},
                    {"stones", 10000},
                    {"metal.ore", 15000},
                    {"sulfur.ore", 15000},
                    {"smg.2", 1},
                    {"ammo.pistol.hv", 150},
                    {"rocket.launcher", 1},
                    {"money", 10000}
                };
                PrintWarning("Failed to load data file, generating a new one...");
            }
        }

        void Unload()
        {
            eventpause?.Destroy();
            eventstart?.Destroy();
            runningman = null;
            eventpause = null;
            eventstart = null;
        }

        void Reload()
        {
            eventpause?.Destroy();
            eventstart?.Destroy();
            runningman = null;
            eventpause = null;
            eventstart = null;
        }

        private void Startevent()
        {
            if (eventpause != null)
            {
                eventpause.Destroy();
                runningman = null;
                Runlog("timer eventpause stopped");
            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            if (BasePlayer.activePlayerList != null && BasePlayer.activePlayerList.Count >= (int) Config["Default", "Count"])
            {
                var t = BasePlayer.activePlayerList;
                if (t == null)
                    return;
                var randI = rnd.Next(1, t.Count);
                runningman = t[randI];
                Runlog("Running man: " + runningman.displayName);
                BroadcastChat((string) Config["Default", "ChatName"], "Running man: " + runningman.displayName);
                BroadcastChat((string) Config["Default", "ChatName"], "Kill him and get the reward!");
                BroadcastChat((string) Config["Default", "ChatName"], "Command: /run - to know the distance to the target");
                eventstart = timer.Once(60*(int) Config["Default", "StarteventTime"], Runningstop);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            else
            {
                BroadcastChat((string) Config["Default", "ChatName"], "There aren't enough players to start the event");
                eventpause?.Destroy();
                eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
        }

        void Runningstop()
        {
            Runlog("Running man - " + runningman.displayName + " ran away from the chase and received as a reward!");

            BroadcastChat("Running man - " + runningman.displayName +
                          " ran away from the chase and received as a reward!");
            var inv = runningman.inventory;
            if ((string) Config["Config", "Reward", "Random"] == "true")
            {
                if (SavedReward == null)
                {
                    PrintWarning("Reward list is empty, please add items");
                    inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                    return;
                }
                Runlog("random");
                var rand = SavedReward.ElementAt(rnd.Next(0, SavedReward.Count));
                switch (rand.Key)
                {
                    case "karma":
                        if (KarmaSystem != null && KarmaSystem.IsLoaded)
                        {
                            IPlayer player = covalence.Players.FindPlayerById(runningman.UserIDString);
                            KarmaSystem.Call("AddKarma", player, GetConfig<double>(1, "Config", "Reward", "KarmaSystem", "PointToAdd"));
                        }
                        else
                        {
                            inv?.GiveItem(ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                                (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
                        }
                        break;
                    case "money":
                        if (Economics != null && Economics.IsLoaded)
                        {
                            Economics?.CallHook("Deposit", runningman.userID,
                                rand.Value);
                        }
                        else
                        {
                            inv?.GiveItem(
                                ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                                    (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
                        }
                        break;
                    default:
                        Runlog(rand.Key);
                        Item item = ItemManager.CreateByName(rand.Key,
                            rand.Value);
                        if(item != null)
                            inv?.GiveItem(item, inv.containerMain);
                        else
                            PrintError($"Failed to create item...{rand.Key}");
                        break;
                }
            }
            else
            {
                Runlog("reward");
                inv?.GiveItem(ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                    (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
            }
            eventstart.Destroy();
            eventstart = null;
            runningman = null;
            Runlog("timer eventstart stopped");
            eventpause?.Destroy();
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void BroadcastChat(string prefix, string msg = null) => rust.BroadcastChat(msg == null ? prefix : "<color=#C4FF00>" +
        prefix + "</color>: " + msg) ;

        private void Runlog(string text)
        {
            Puts("[EVENT] +--------------- RUNNING MAN -----------------");
            Puts("[EVENT] | "+text);
            Puts("[EVENT] +---------------------------------------------");
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (runningman != null)
                if (player == runningman)
                {
                    stillRunnerTimer = timer.Once(60*(int) Config["Default", "DisconnectPendingTimer"], DestroyLeaveEvent);
                }
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (runningman != null)
            {
                if (runningman == player)
                {
                    SendReply(player,
                        (string) Config["Default", "ChatName"] + ": " + runningman.displayName +
                        " your are still the runner.");
                    BroadcastChat("Running man: ", runningman.displayName + " is back online.");
                    BroadcastChat("Kill him and get the reward!");
                    stillRunnerTimer?.Destroy();
                }
                else
                {
                    SendHelpText(player);
                }
            }
        }

        void PlayerKilled(BasePlayer victim, HitInfo hitinfo)
        {
            var attacker = hitinfo?.Initiator?.ToPlayer();
            if (attacker == null) return;
            if (victim == null) return;
            if (attacker == victim) return;
            if (runningman == null) return;
            if(victim != runningman) return;
            Runlog("Running man - " + attacker.displayName + " kill " + runningman.displayName +
                   " and received as a reward!");
            BroadcastChat("Player - " + attacker.displayName +
                          " kill " + runningman.displayName +
                          " and received as a reward!");
            var inv = attacker.inventory;
            if ((string) Config["Config", "Reward", "Random"] == "true")
            {
                if (SavedReward == null)
                {
                    PrintWarning("Reward list is empty, please add items, using FixingReward option...");
                    inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                        (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
                    return;
                }
                var rand = SavedReward.ElementAt(rnd.Next(0, SavedReward.Count));
                switch (rand.Key)
                {
                    case "karma":
                        if (KarmaSystem != null && KarmaSystem.IsLoaded)
                        {
                            IPlayer player = covalence.Players.FindPlayerById(attacker.UserIDString);
                            KarmaSystem.Call("AddKarma", player, GetConfig<double>(1, "Config", "Reward", "KarmaSystem", "PointToAdd"));
                        }
                        else
                        {
                            inv?.GiveItem(ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                                (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
                        }
                        break;
                    case "money":
                        if (Economics != null && Economics.IsLoaded)
                        {
                            Economics?.CallHook("Deposit", runningman.userID,
                                rand.Value);
                        }
                        else
                        {
                            inv?.GiveItem(
                                ItemManager.CreateByName((string) Config["Config", "Reward", "RewardFixing"],
                                    (int) Config["Config", "Reward", "RewardFixingAmount"]), inv.containerMain);
                        }
                        break;
                    default:
                        Runlog(rand.Key);
                        Item item = ItemManager.CreateByName(rand.Key,
                            rand.Value);
                        if(item != null)
                            inv?.GiveItem(item, inv.containerMain);
                        else
                            PrintError($"Failed to create item...{rand.Key}");
                        break;
                }
            }
            else
            {
                Puts(Config["Reward", "RewardFixing"].ToString());
                inv?.GiveItem(ItemManager.CreateByName((string) Config["Reward", "RewardFixing"],
                    (int) Config["Reward", "RewardFixingAmount"]), inv.containerMain);
            }
            eventstart?.Destroy();
            eventstart = null;
            runningman = null;
            Runlog("timer eventstart stopped");
            eventpause?.Destroy();
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (entity.ToPlayer())
                PlayerKilled(entity.ToPlayer(), hitinfo);
        }

        void cmdRun(BasePlayer player, string cmd, string[] args)
        {
            if (!player)
                return;
            if (runningman != null)
            {
                var xr = runningman.transform.position.x;
                var zr = runningman.transform.position.z;
                var xk = player.transform.position.x;
                var zk = player.transform.position.z;
                var dist = Math.Floor(Math.Sqrt(Math.Pow(xr - xk, 2) + Math.Pow(zr - zk, 2)));
                SendReply(player, (string) Config["Default", "ChatName"] + ": " + runningman.displayName + ",");
                SendReply(player, (string) Config["Default", "ChatName"] + ": is at a distance of " + dist + "м");
                SendReply(player, (string) Config["Default", "ChatName"] + ": Kill him and get the reward!");
                time2 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                var time3 = time2 - time1;
                time3 = eventstart.Delay - time3;
                time3 = Math.Floor(time3/60);
                SendReply(player, (string) Config["Default", "ChatName"] + ": Until the end of event left: " + time3 + " minutes");
            }
            else
            {
                time2 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                var time3 = time2 - time1;
                if (eventpause != null)
                {
                    time3 = eventpause.Delay - time3;
                    time3 = Math.Floor(time3/60);
                    SendReply(player, (string) Config["Default", "ChatName"] + ": At the moment the event is not running");
                    SendReply(player,
                        (string) Config["Default", "ChatName"] + ": Before the start of the event remained: " + time3 +
                        " minutes");
                }
                else
                {
                    SendReply(player, (string) Config["Default", "ChatName"] + ": At the moment the event is not running");
                }
            }
        }

        void SendHelpText(BasePlayer player)
        {
            player.ChatMessage("Use \"/run\" to find out information about the running man");
            var authlevel = player.net.connection.authLevel;
            if (authlevel >= (int) Config["Default", "authLevel"]) 
            {
                player.ChatMessage("Use \"/eventon\" for start event Running Man");
                player.ChatMessage("Use \"/eventoff\" for stop event Running Man");
            }
        }

        void cmdEvent(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel >= (int) Config["Default", "authLevel"])
            {
                if (eventpause != null)
                {
                    eventpause.Destroy();
                    eventpause = null;
                    runningman = null;
                    Runlog("timer eventpause stopped");
                }
                if (eventstart != null)
                {
                    eventstart.Destroy();
                    eventstart = null;
                    runningman = null;
                    Runlog("timer eventstart stopped");
                }
                List<BasePlayer> onlineplayers = BasePlayer.activePlayerList;
                if (onlineplayers == null)
                {
                    SendReply(player, "You can't run event while there is nobody online.");
                    return;
                }
                var randI = rnd.Next(0, onlineplayers.Count);
                runningman = onlineplayers[randI];
                Runlog("Running man: " + runningman.displayName);
                BroadcastChat("Running man: ", runningman.displayName);
                BroadcastChat("Kill him and get the reward!");
                BroadcastChat("Command: /run - to know the distance to the target\"");
                eventstart = timer.Once(60*(int) Config["Default", "StarteventTime"], Runningstop);
                time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            else
                SendReply(player, Config["Default", "ChatName"] + ": You have no rights to do this!");
        }

        void ccmdEvent(ConsoleSystem.Arg arg)
        {
            if (eventpause != null)
            {
                eventpause.Destroy();
                runningman = null;
                Runlog("timer eventpause stopped");
            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            List<BasePlayer> onlineplayers = BasePlayer.activePlayerList;
            var randI = rnd.Next(0, onlineplayers.Count);
            runningman = onlineplayers[randI];
            Runlog("Running man: " + runningman.displayName);
            BroadcastChat("Running man: ", runningman.displayName);
            BroadcastChat("Kill him and get the reward!\"");
            BroadcastChat("Command: /run - to know the distance to the target\"");
            eventstart = timer.Once(60*(int) Config["Default", "StarteventTime"], Runningstop);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        void cmdEventOf(ConsoleSystem.Arg arg)
        {
            DestroyEvent();
        }

        void DestroyEvent()
        {
            if (eventpause != null)
            {
                eventpause.Destroy();
                eventpause = null;
                runningman = null;
                Runlog("timer eventpause stopped");

            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                eventstart = null;
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            Runlog("Running Man has stopped");
        }

        void DestroyLeaveEvent()
        {
            Runlog("Player " + runningman.displayName + " got scared and ran away!");
            BroadcastChat("Player " + runningman.displayName + " got scared and ran away!");
            if (eventpause != null)
            {
                eventpause.Destroy();
                eventpause = null;
                runningman = null;
                Runlog("timer eventpause stopped");

            }
            if (eventstart != null)
            {
                eventstart.Destroy();
                eventstart = null;
                runningman = null;
                Runlog("timer eventstart stopped");
            }
            Runlog("Running Man has stopped");
            eventpause = timer.Once(60*(int) Config["Default", "PauseeventTime"], Startevent);
            time1 = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }
        void cmdEventOff(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel >= (int) Config["Default", "authLevel"])
            {
                if (eventpause != null)
                {
                    eventpause.Destroy();
                    eventpause = null;
                    runningman = null;
                    Runlog("timer eventpause stopped");
                }
                if (eventstart != null)
                {
                    eventstart.Destroy();
                    eventstart = null;
                    runningman = null;
                    Runlog("timer eventstart stopped");
                }
                Runlog("Running Man has stopped");
                rust.SendChatMessage(player, Config["Default", "ChatName"] + ": Event has stopped!");
            }
            else
                rust.SendChatMessage(player, Config["Default", "ChatName"] + ": You have no rights to do this!");
        }
    }
}