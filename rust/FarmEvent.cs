using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("FarmEvent", "Hougan", "1.0.1")]
    [Description("In-game event, farming resources with GUI, Log, TOP, e.t.c.")]
    class FarmEvent : RustPlugin
    {
        #region Variable
        private Dictionary<ulong, int> playerFarm = new Dictionary<ulong, int>();
        private Dictionary<string, DateTime> playerComplete = new Dictionary<string, DateTime>();
        private string adminPermission = "farmevent.start";
        private Event currentEvent = new Event();

        class Event
        {
            public string Name = "Default";
            public string Item;
            public Timer Time;
            public Timer Broad;
            public int Goal;
            public int Places;
        }

        private int broadcastInterval;
        

        #endregion

        #region Commands
        [ChatCommand("fe.start")]
        void eventStart(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, adminPermission)) { SendReply(player, msg("Permission", player.UserIDString)); return;}
            if (currentEvent.Name != "Default") { SendReply(player, msg("Started", player.UserIDString)); return; }
            if (args.Length != 5) { SendReply(player, msg("Syntax", player.UserIDString)); return; }
            currentEvent.Name = args[0];
            currentEvent.Item = args[1];
            currentEvent.Goal = Int32.Parse(args[2]);
            currentEvent.Places = Int32.Parse(args[3]);
            currentEvent.Time = timer.Once(Int32.Parse(args[4]), () => { stopEvent(); });
            currentEvent.Broad = timer.Every(broadcastInterval, () => { Server.Broadcast(String.Format(msg("DO_NOT_CHANGE"), eventStat())); });
            foreach (var check in BasePlayer.activePlayerList)
            {
                if (!playerFarm.ContainsKey(check.userID))
                    playerFarm.Add(check.userID, 0);
                updateGUI(check);
            }
            Server.Broadcast(String.Format(msg("Start"), currentEvent.Goal, ItemManager.FindItemDefinition(currentEvent.Item).displayName.english));
            LogToFile("FarmEvent", "--------------------------------", this, false);
            LogToFile("FarmEvent",
                $"[Name: {currentEvent.Name}] Event started at: {DateTime.Now} | Farm: {currentEvent.Item}\nGoal: {currentEvent.Goal} | Time: {args[4]} | Started by: {player.net.connection.userid}\n\n", this, false);
        }
        [ChatCommand("fe.stop")]
        void eventStop(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, adminPermission))
            {
                SendReply(player, msg("Permission", player.UserIDString));
                return;
            }
            if (currentEvent.Name == "Default") { SendReply(player, msg("Stopped", player.UserIDString)); return; }
            if (args.Length != 0) { SendReply(player, msg("SyntaxS", player.UserIDString)); return; }
            stopEvent();
        }

        [ChatCommand("fe.stat")]
        void playerEventStat(BasePlayer player)
        {
            if (currentEvent.Name != "Default")
                SendReply(player, string.Format(msg("DO_NOT_CHANGE"), eventStat()));
        }
        #endregion

        #region Function
        void stopEvent()
        {
            Server.Broadcast(string.Format(msg("DO_NOT_CHANGE"), eventStat(true)));
            LogToFile("FarmEvent", "Event results:", this, false);
            LogToFile("FarmEvent", String.Format(msg("DO_NOT_CHANGE"), eventStat()), this, false);
            LogToFile("FarmEvent", "--------------------------------", this, false);
            currentEvent.Time.Destroy();
            currentEvent.Broad.Destroy();
            currentEvent = new Event();
            playerFarm.Clear();
            playerComplete.Clear();
            foreach (var check in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(check, "FarmGUI");
        }

        string eventStat(bool Finish = false)
        {
            string result = "";
            if (!Finish)
                result = string.Format(msg("WinnerHeader"));
            else
                result = string.Format(msg("Finnish"));
            int i = 1;
            foreach (var check in playerComplete)
            {
                result += String.Format(msg("WinnerPlace"), i, check.Key, check.Value.ToShortTimeString());
                i++;
            }
            foreach (var uid in playerFarm.OrderByDescending(p => p.Value).Take(currentEvent.Places - i + 1))
            {
                bool foundSorryForUselessMethodToCheck = false;
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (check.net.connection.userid == uid.Key)
                    {
                        result += String.Format(msg("EncouragingPlace"), i, BasePlayer.FindByID(uid.Key).net.connection.username, uid.Value);
                        foundSorryForUselessMethodToCheck = true;
                    }
                }
                if (!foundSorryForUselessMethodToCheck)
                    result += String.Format(msg("EncouragingPlace"), i, uid.Key, uid.Value);
                foundSorryForUselessMethodToCheck = false;
                i++;
            }
            result += msg("Congratulation");
            return result;
        }
        object GetVariable(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
            }
            return value;
        }
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion

        #region Hooks
        protected override void LoadDefaultConfig()
        {
            broadcastInterval = Convert.ToInt32(GetVariable("Main", "Auto-broadcast interval", 30));
            SaveConfig();
        }
        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(adminPermission, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["WinnerHeader"] = "Current event <color=#DC143C>results</color>:\n",
                ["Finnish"] = "Event <color=#DC143C>ENDED</color>, results:\n",
                ["WinnerPlace"] = "\n[<color=#DC143C>{0}</color>] <color=#b8b8b8>{1}</color> finished at <color=#DC143C>{2}</color>!",
                ["EncouragingPlace"] = "\n[<color=#b8b8b8>{0}</color>] <color=#b8b8b8>{1}</color> farmed <color=#DC143C>{2}</color>!",
                //
                ["Finished"] = "Congratulation! <color=#DC143C>{0}</color> finished at <color=#DC143C>{1}</color>",
                ["LFinished"] = "Congratulation! <color=#DC143C>{0}</color> finished at <color=#DC143C>{1}</color>, but it is too <color=#DC143C>late</color>",
                //
                ["Start"] = "Event started, you should farm <color=#DC143C>{0}</color>x of <color=#DC143C>{1}</color>!",
                ["Congratulation"] = "\n\nThank you for <color=#DC143C>participating</color>!",
                //
                ["Permission"] = "You have no permission!",
                ["Syntax"] = "Use: /fe.start EventNAME ResourceNAME ResourcesAMOUNT WinPlacesAMOUNT TIMEINSEC!",
                ["SyntaxS"] = "Use: /fe.stop!",
                ["Started"] = "Already started, stop!",
                ["Stopped"] = "Already stopped, start!",
                ["DO_NOT_CHANGE"] = "{0}"
            }, this);
        }

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (currentEvent.Name != "Default" && !playerComplete.ContainsKey(entity.net.connection.username))
            {
                if (item.info.shortname == currentEvent.Item)
                {

                    playerFarm[entity.GetComponent<BasePlayer>().net.connection.userid] += item.amount;
                    if (playerFarm[entity.GetComponent<BasePlayer>().net.connection.userid] >= currentEvent.Goal)
                    {
                        if (playerComplete.Count < currentEvent.Places)
                        {
                            playerComplete.Add(entity.GetComponent<BasePlayer>().net.connection.username, DateTime.Now);
                            CuiHelper.DestroyUi(entity.GetComponent<BasePlayer>(), "FarmGUI");
                            Server.Broadcast(String.Format(msg("Finished"), entity.GetComponent<BasePlayer>().net.connection.username, DateTime.Now.ToShortTimeString()));
                            playerFarm.Remove(entity.GetComponent<BasePlayer>().net.connection.userid);
                            return;
                        }
                        else
                        {
                            CuiHelper.DestroyUi(entity.GetComponent<BasePlayer>(), "FarmGUI");
                            return;
                        }
                    }
                    updateGUI(entity.GetComponent<BasePlayer>());
                }
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!playerFarm.ContainsKey(player.userID))
                playerFarm.Add(player.userID, 0);
            if (currentEvent.Name != "Default")
                updateGUI(player);
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (currentEvent.Name != "Default")
                updateGUI(player);
        }
        #endregion

        #region GUI
        private void updateGUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "FarmGUI");
            FarmGUI(player);
        }
        private void FarmGUI(BasePlayer player)
        {
            var FarmElements = new CuiElementContainer();
            var Choose = FarmElements.Add(new CuiPanel
            {
                Image = { Color = $"0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.0078125 0.002777774", AnchorMax = "0.1838542 0.03518518" },
                CursorEnabled = false,
            }, "HUD", "FarmGUI");
            FarmElements.Add(new CuiButton
            {
                Button = { Color = $"0.34 0.34 0.34 0", Close = Choose },
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1" },
                Text = { Text = $"You farmed: <color=#DC143C>{playerFarm[player.userID]}</color> {ItemManager.FindItemDefinition(currentEvent.Item).displayName.english}  [<color=#DC143C>{currentEvent.Goal - playerFarm[player.userID]}</color> left]", Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", FontSize = 14 }
            }, Choose);

            CuiHelper.AddUi(player, FarmElements);
        }
        #endregion

        #region Debug
        [ChatCommand("fe.fake")]
        void fakeTest(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { return; }
            playerFarm.Add(ulong.Parse(args[0]), Int32.Parse(args[1]));
        }
        #endregion
    }
}