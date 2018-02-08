using Oxide.Core;
using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AntiNoobRaid", "Slydelix", "1.5.1", ResourceId = 2697)]
    class AntiNoobRaid : RustPlugin
    {
        [PluginReference] Plugin PlaytimeTracker;
        [PluginReference] Plugin WipeProtection;

        //set this to true if you are having issues with the plugin
        private bool debug = false;

        private List<BasePlayer> cooldown = new List<BasePlayer>();
        private Dictionary<string, string> raidtools = new Dictionary<string, string>
        {
            {"ammo.rocket.fire", "rocket_fire"},
            {"ammo.rocket.hv", "rocket_hv"},
            {"ammo.rocket.basic", "rocket_basic"},
            {"explosive.timed", "explosive.timed.deployed"},
            {"surveycharge", "survey_charge.deployed"},
            {"explosive.satchel", "explosive.satchel.deployed"},
            {"grenade.beancan", "grenade.beancan.deployed"},
            {"grenade.f1", "grenade.f1.deployed"}
        };

        private int layers = LayerMask.GetMask("Construction", "Deployed");
        private int time, refundTimes, frequency;
        private double steamInGameTime;
        private bool show, showTime, refund, preventnew, unnoobnew, checkSteam, msgonfirstconnection, useGT;
        private string apiKey = "";

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config["Notify player on first connection with protection time"] = msgonfirstconnection = GetConfig("Notify player on first connection with protection time", true);
            Config["Use game tips to send first connection message to players"] = useGT = GetConfig("Use game tips to send first connection message to players", false);
            Config["Time inside which new players are protected"] = time = GetConfig("Time inside which new players are protected", 86400);
            Config["Prevent new players from raiding"] = preventnew = GetConfig("Prevent new players from raiding", false);
            Config["Remove noob status of a raider on raid attempt"] = unnoobnew = GetConfig("Remove noob status of a raider on raid attempt", false);
            Config["Show message for not being able to raid"] = show = GetConfig("Show message for not being able to raid", true);
            Config["Show time until raidable"] = showTime = GetConfig("Show time until raidable", false);
            Config["User data refresh interval (seconds)"] = frequency = GetConfig("User data refresh interval (seconds)", 30);
            Config["Refund explosives"] = refund = GetConfig("Refund explosives", true);
            Config["Check Steam for in game time"] = checkSteam = GetConfig("Check Steam for in game time", true);
            Config["Steam API key"] = apiKey = GetConfig("Steam API key", "");
            Config["In-game steam time which mark player as non-noob (hours)"] = steamInGameTime = GetConfig("In-game steam time which mark player as non-noob (hours)", 200);
            Config["Refunds before player starts losing explosives"] = refundTimes = GetConfig("Refunds before player starts losing explosives", 1);
            SaveConfig();
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"console_removenewhelp", "Wrong syntax! antinoob.removenoob <steamID>"},
                {"console_setnot", "Set {0} as a not-new player"},
                {"console_alreadynot", "That player is already a marked as non noob"},
                {"console_newhelp", "Wrong syntax! antinoob.addnoob <steamID>"},
                {"console_setnew", "Set {0} as a new player"},
                {"console_alreadynew", "That player is already a marked as noob"},
                {"console_wrongSteamID", "Wrong steamID"},
                {"console_cantfind", "Couldn't find a player with that ID"},
                {"firstconnectionmessage", "You are a new player so your buildings are protected for first {0} hours of your time on server"},
                {"steam_checkstart", "{0} connected, checking Steam"},
                {"steam_marking", "Marking {0} as non noob"},
                {"steam_connected", "{0} has connected with {1} in game hours"},
                {"steam_private", "Steam profile of {0} is private"},
                {"steam_responsewrong", "Failed to contact steam API, profile is private/wrong API key"},
                {"steam_wrongapikey", "Invalid API key"},
                {"pt_notInstalled_first", "Playtime Tracker is not installed, will check again in 30 seconds"},
                {"pt_notInstalled", "Playtime Tracker is not installed!"},
                {"pt_detected", "Playtime Tracker detected"},
                {"userinfo_nofound", "Failed to get playtime info for {0}! trying again in 20 seconds!"},
                {"userinfo_nofoundmsg", "Failed to find playtime info for player with steamID {0}"},
                {"userinfo_found", "Found info for {0}"},
                {"can_attack", "This structure is not raid protected"},
                {"NotLooking", "You are not looking at a building/deployable"},
                {"dataFileWiped", "Data file successfully wiped"},
                {"dataFileWiped_attempts", "Data file successfully wiped (raid attempts)"},
                {"dataFileWiped_playerdata", "Data file successfully wiped (player data)"},
                {"refund_free", "Your '{0}' was refunded."},
                {"refunditem_added", "Added '{0}' to list of items to refund"},
                {"refunditem_help", "Wrong Syntax! /refunditem add <you have to hold the item you want to add>\n/refunditem remove <you have to hold the item you want to remove>\n/refunditem list\n/refunditem clear\n/refunditem all <sets all raid tools as refundable>"},
                {"refunditem_noperm", "You don't have permission to use this command."},
                {"refunditem_needholditem", "You need to hold the item you want to add/remove from refund list"},
                {"refunditem_notexplosive", "This item is not an explosive"},
                {"refunditem_alreadyonlist", "This item is already on the list"},
                {"refunditem_notonlist", "This item is not on the list"},
                {"refunditem_removed", "Removed '{0}' from the list of items to refund"},
                {"refunditem_addedall", "Added all raid tools to refund list"},
                {"refunditem_cleared", "Cleared list of items to refund"},
                {"refunditem_empty", "There are no item set up yet"},
                {"refunditem_list", "List of items which will get refunded: \n{0}"},
                {"refund_last", "Your '{0}' was refunded but will not be next time."},
                {"refund_1time", "Your '{0}' was refunded After 1 more attempt it wont be refunded."},
                {"refund_nTimes", "Your '{0}' was refunded. After {1} more attempts it wont be refunded"},
                {"cannot_attack_no_time", "This entity cannot be destroyed because it was built by a new player"},
                {"cannot_attack_new_raider", "Because you are a new player you cannot raid (yet)"},
                {"cannot_attack_time", "This entity cannot be destroyed because it was built by a new player ({0})"},
                {"secs", " seconds"},
                {"mins", " minutes"},
                {"min", " minute"},
                {"hours", " hours"},
                {"hour", " hour"},
                {"day", " day"},
                {"days", " days"}
            }, this);
        }

        #endregion
        #region DataFile

        private class StoredData
        {
            public Dictionary<ulong, double> players = new Dictionary<ulong, double>();
            public Dictionary<ulong, int> AttackAttempts = new Dictionary<ulong, int>();
            public Dictionary<string, string> ItemList = new Dictionary<string, string>();
            public List<ulong> playersWithNoData = new List<ulong>();
            public List<ulong> FirstMessaged = new List<ulong>();

            public StoredData()
            {
            }
        }

        StoredData storedData;

        #endregion
        #region Steam
        private class steamGames
        {
            [JsonProperty("response")]
            public Content response;

            public class Content
            {
                [JsonProperty("game_count")]
                public int game_count;
                [JsonProperty("games")]
                public Game[] games;

                public class Game
                {
                    [JsonProperty("appid")]
                    public uint appid;
                    [JsonProperty("playtime_2weeks")]
                    public int playtime_2weeks;
                    [JsonProperty("playtime_forever")]
                    public int playtime_forever;
                }
            }
        }

        private T Deserialise<T>(string file) => JsonConvert.DeserializeObject<T>(file);

        #endregion
        #region Hooks

        private void Unload() => SaveFile();

        private void Loaded()
        {
            StartChecking();
            CheckPlayersWithNoInfo();
            if (PlaytimeTracker == null)
            {
                PrintWarning(lang.GetMessage("pt_notInstalled_first", this, null));
                timer.In(30f, () => {
                    if (PlaytimeTracker == null)
                    {
                        PrintWarning(lang.GetMessage("pt_notInstalled", this, null));
                        return;
                    }
                    PrintWarning(lang.GetMessage("pt_detected", this, null));
                });
            }
        }

        private void Init()
        {
            permission.RegisterPermission("antinoobraid.admin", this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            LoadDefaultConfig();
        }

        private void OnUserConnected(IPlayer player)
        {
            BasePlayer bp = player.Object as BasePlayer;
            FirstMessage(bp);
            if (checkSteam) SteamCheck(bp, true);

            if (storedData.players.ContainsKey(bp.userID))
                if (storedData.players[bp.userID] == -50d || storedData.players[bp.userID] == -25d) return;

            double time = -1d;
            try
            {
                time = PlaytimeTracker?.Call<double>("GetPlayTime", bp.UserIDString) ?? -1d;
                if (time == -1d)
                {
                    Puts(lang.GetMessage("pt_notInstalled", this, null));
                    return;
                }
            }

            catch (Exception exc)
            {
                Puts(lang.GetMessage("userinfo_nofound", this, null), bp.userID);
                timer.In(20f, () => {
                    Check(bp.userID);
                });

            }

            if (storedData.playersWithNoData.Contains(bp.userID)) storedData.playersWithNoData.Remove(bp.userID);

            if (storedData.players.ContainsKey(bp.userID))
            {
                storedData.players[bp.userID] = time;
                SaveFile();
                return;
            }

            storedData.players.Add(bp.userID, time);
            SaveFile();
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo == null || entity == null || hitinfo?.InitiatorPlayer == null || entity.OwnerID == hitinfo?.InitiatorPlayer?.userID || entity?.OwnerID == 0 || hitinfo?.WeaponPrefab?.ShortPrefabName == null) return null;
            if (!(entity is BuildingBlock || entity is Door || entity.PrefabName.Contains("deployable"))) return null;

            bool wipe = WipeProtection?.Call<bool>("WipeProtected") ?? false;
            if (wipe) return null;

            BasePlayer attacker = hitinfo.InitiatorPlayer;

            if (cooldown.Contains(attacker))
            {
                RemoveCD(attacker);
                if (playerIsNew(entity.OwnerID))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                }
                return null;
            }
            //TBH I'm confused as same as you at this point
            cooldown.Add(attacker);
            RemoveCD(attacker);
            logPlayer(attacker);

            string name = hitinfo?.WeaponPrefab?.ShortPrefabName ?? "Null";

            if (debug) SendReply(attacker, "Name: " + name);

            if (!storedData.players.ContainsKey(attacker.userID))
            {
                Puts("This shouldn't happen (check at containskey of attacker userID)");
                Check(attacker.userID);
                return null;
            }

            if (preventnew)
            {
                if (playerIsNew(attacker.userID))
                {
                    if (unnoobnew)
                    {
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                        hitinfo.HitMaterial = 0;
                        storedData.players[attacker.userID] = -50d;
                        SaveFile();
                        NextTick(() => {
                            SendReply(attacker, lang.GetMessage("cannot_attack_new_raider", this, attacker.UserIDString));
                            Refund(attacker, name, entity);
                            //msgPlayer(attacker, entity);
                        });
                        return true;
                    }

                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    SendReply(attacker, lang.GetMessage("cannot_attack_new_raider", this, attacker.UserIDString));
                    Refund(attacker, name, entity);
                    return true;
                }
            }

            if (storedData.players.ContainsKey(entity.OwnerID))
            {
                if (playerIsNew(entity.OwnerID))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    NextTick(() => {
                        msgPlayer(attacker, entity);
                        Refund(attacker, name, entity);
                    });
                    return true;
                }
                return null;
            }
            //DEBUGGING!
            string date = "[" + DateTime.Now.ToString() + "] ";
            Puts("No owner found for entity at " + entity.transform.position + " , attacker " + attacker.userID + " (" + entity.ShortPrefabName + ")");
            LogToFile(this.Name, date + "No owner found for entity at " + entity.transform.position + " , attacker " + attacker.userID, this, true);
            Check();
            return null;
        }

        #endregion
        #region Other Stuff

        private void FirstMessage(BasePlayer player)
        {
            if (!msgonfirstconnection) return;

            if (storedData.FirstMessaged.Contains(player.userID)) return;

            storedData.FirstMessaged.Add(player.userID);
            SaveFile();

            if (storedData.players.ContainsKey(player.userID))
            {
                //notsure
                if (storedData.players[player.userID] > 100d || storedData.players[player.userID] == -50d || storedData.players[player.userID] == -25d) return;
            }

            if (useGT)
            {
                //not on by default because is sometimes hardly noticable
                player.SendConsoleCommand("gametip.showgametip", lang.GetMessage("firstconnectionmessage", this, player.UserIDString), time / 3600);
                timer.In(10f, () => {
                    player.SendConsoleCommand("gametip.hidegametip");
                    return;
                });
                return;
            }

            SendReply(player, lang.GetMessage("firstconnectionmessage", this, player.UserIDString), time / 3600);
            return;
        }

        private void RemoveCD(BasePlayer player)
        {
            timer.In(0.1f, () => {
                if (cooldown.Contains(player)) cooldown.Remove(player);
            });
        }

        private void logPlayer(BasePlayer attacker)
        {
            if (!storedData.AttackAttempts.ContainsKey(attacker.userID))
            {
                storedData.AttackAttempts.Add(attacker.userID, 1);
                SaveFile();
                return;
            }

            storedData.AttackAttempts[attacker.userID]++;
            SaveFile();
        }

        private void msgPlayer(BasePlayer attacker, BaseEntity entity)
        {
            double time2 = PlaytimeTracker?.Call<double>("GetPlayTime", entity.OwnerID.ToString()) ?? -1d;
            if (time2 == -1d) return;
            int left = (int)(time - time2);

            if (show)
            {
                if (playerIsNew(entity.OwnerID))
                {
                    if (showTime)
                    {
                        SendReply(attacker, lang.GetMessage("cannot_attack_time", this, attacker.UserIDString), CheckLeft(left));
                        return;
                    }

                    SendReply(attacker, lang.GetMessage("cannot_attack_no_time", this, attacker.UserIDString));
                    return;
                }

                SendReply(attacker, lang.GetMessage("can_attack", this, attacker.UserIDString));
                return;
            }
        }

        private void Refund(BasePlayer attacker, string name, BaseEntity ent)
        {
            //Possibly most f**ked up thing I've ever made
            if (refund)
            {
                if (storedData.ItemList.Count < 1) return;

                foreach (var entry in storedData.ItemList)
                {
                    if (name == entry.Value)
                    {
                        if (refundTimes == 0)
                        {
                            Item item = ItemManager.CreateByName(entry.Key, 1);
                            attacker.GiveItem(item);
                            SendReply(attacker, lang.GetMessage("refund_free", this, attacker.UserIDString), item.info.displayName.english);
                        }

                        else
                        {
                            if ((storedData.AttackAttempts[attacker.userID]) <= refundTimes)
                            {
                                int a = refundTimes - (storedData.AttackAttempts[attacker.userID]);
                                Item item = ItemManager.CreateByName(entry.Key, 1);
                                attacker.GiveItem(item);

                                switch (a)
                                {
                                    case 0:
                                        {
                                            SendReply(attacker, lang.GetMessage("refund_last", this, attacker.UserIDString), item.info.displayName.english);
                                            return;
                                        }

                                    case 1:
                                        {
                                            SendReply(attacker, lang.GetMessage("refund_1time", this, attacker.UserIDString), item.info.displayName.english);
                                            return;
                                        }

                                    default:
                                        {
                                            SendReply(attacker, lang.GetMessage("refund_nTimes", this, attacker.UserIDString), item.info.displayName.english, a);
                                            return;
                                        }

                                }
                            }
                        }
                    }
                }
            }
        }

        private void CheckPlayersWithNoInfo()
        {
            int rate = (frequency <= 10) ? 10 : frequency - 10;

            timer.Every(rate, () =>
            {
                if (storedData.playersWithNoData.Count < 1) return;

                foreach (ulong ID in storedData.playersWithNoData.ToList())
                {
                    double time = -1d;
                    try
                    {
                        time = PlaytimeTracker?.Call<double>("GetPlayTime", ID.ToString()) ?? -1d;
                        if (time == -1d)
                        {
                            Puts(lang.GetMessage("pt_notInstalled", this, null));
                            return;
                        }
                    }

                    catch (Exception exc)
                    {
                        //PTT doesn't contain info for the player
                        continue;
                    }

                    if (storedData.players.ContainsKey(ID))
                    {
                        //This isn't supposed to happen
                        string date = "[" + DateTime.Now.ToString() + "] ";
                        LogToFile(this.Name, date + "Somehow info exists for player that is in data file already (" + ID + ")", this, true);
                        storedData.players[ID] = time;
                        storedData.playersWithNoData.Remove(ID);
                        SaveFile();
                        continue;
                    }

                    storedData.players.Add(ID, time);
                    storedData.playersWithNoData.Remove(ID);
                    SaveFile();
                    continue;
                }
            });
        }

        private void Check(ulong ID)
        {
            if (storedData.playersWithNoData.Contains(ID)) return;
            if (storedData.players[ID] == -50d || storedData.players[ID] == -25d) return;

            double time = -1d;
            try
            {
                time = PlaytimeTracker?.Call<double>("GetPlayTime", ID.ToString()) ?? -1d;
            }

            catch (Exception exc)
            {
                Puts(lang.GetMessage("userinfo_nofoundmsg", this, null), ID);
                storedData.playersWithNoData.Add(ID);
                SaveFile();
            }

            if (time == -1d)
            {
                Puts(lang.GetMessage("pt_notInstalled", this, null));
                return;
            }

            if (!storedData.players.ContainsKey(ID))
            {
                storedData.players.Add(ID, time);
                SaveFile();
                Puts(lang.GetMessage("userinfo_found", this, null), ID);
                return;
            }

            storedData.players[ID] = time;
            SaveFile();
            Puts(lang.GetMessage("userinfo_found", this, null), ID);
        }

        private void Check()
        {
            if (BasePlayer.activePlayerList.Count > 0)
            {
                foreach (BasePlayer bp in BasePlayer.activePlayerList)
                {
                    if (!bp.IsConnected || bp == null) continue;
                    if (storedData.playersWithNoData.Contains(bp.userID)) continue;
                    if (storedData.players.ContainsKey(bp.userID))
                    {
                        if (storedData.players[bp.userID] == -50d || storedData.players[bp.userID] == -25d) continue;
                    }

                    double time = -1d;
                    try
                    {
                        time = PlaytimeTracker?.Call<double>("GetPlayTime", bp.UserIDString) ?? -1d;
                    }

                    catch (Exception exc)
                    {
                        Puts(lang.GetMessage("userinfo_nofoundmsg", this, null), bp.userID);
                        storedData.playersWithNoData.Add(bp.userID);
                        SaveFile();
                    }

                    if (time == -1d)
                    {
                        Puts(lang.GetMessage("pt_notInstalled", this, null));
                        return;
                    }

                    if (!storedData.players.ContainsKey(bp.userID))
                    {
                        storedData.players.Add(bp.userID, time);
                        SaveFile();
                        continue;
                    }

                    storedData.players[bp.userID] = time;
                    SaveFile();
                    continue;
                }
            }

            foreach (BasePlayer bp in BasePlayer.sleepingPlayerList)
            {
                if (storedData.playersWithNoData.Contains(bp.userID)) continue;
                if (storedData.players.ContainsKey(bp.userID))
                {
                    if (storedData.players[bp.userID] == -50d || storedData.players[bp.userID] == -25d) continue;
                }

                double time = -1d;
                try
                {
                    time = PlaytimeTracker?.Call<double>("GetPlayTime", bp.UserIDString) ?? -1d;
                }

                catch (Exception exc)
                {
                    Puts(lang.GetMessage("userinfo_nofoundmsg", this, null), bp.userID);
                    storedData.playersWithNoData.Add(bp.userID);
                    SaveFile();
                }

                if (time == -1d)
                {
                    Puts(lang.GetMessage("pt_notInstalled", this, null));
                    return;
                }

                if (!storedData.players.ContainsKey(bp.userID))
                {
                    storedData.players.Add(bp.userID, time);
                    SaveFile();
                    continue;
                }

                storedData.players[bp.userID] = time;
                SaveFile();
                continue;
            }
        }

        private void SteamCheck(BasePlayer bp, bool connecting)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Puts(lang.GetMessage("steam_wrongapikey", this, null));
                return;
            }

            string date = "[" + DateTime.Now.ToString() + "] ";
            if (bp == null)
            {
                Puts("BasePlayer is null?? Report this on oxide (at SteamCheck) " + bp);
                return;
            }

            if (storedData.players.ContainsKey(bp.userID))
            {
                if (storedData.players[bp.userID] == -50d)
                {
                    LogToFile(this.Name, date + "Player " + bp + " is already marked as non noob", this, false);
                    return;
                }

                else if (storedData.players[bp.userID] == -25d)
                {
                    LogToFile(this.Name, date + "Player " + bp + " is already marked as noob (-25)", this, false);
                    return;
                }
            }

            Puts(lang.GetMessage("steam_checkstart", this, null), bp);

            webrequest.Enqueue("http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key=" + apiKey + "&steamid=" + bp.UserIDString, null, (code, response) =>
            {
                if (code != 200)
                {
                    Puts(lang.GetMessage("steam_responsewrong", this, null));
                    LogToFile(this.Name, date + "Wrong response for " + bp + " (" + code + ")", this, false);
                    return;
                }

                var des = Deserialise<steamGames>(response);
                if (des?.response?.games == null)
                {
                    Puts(lang.GetMessage("steam_private", this, null), bp);
                    LogToFile(this.Name, date + "Steam profile of " + bp + " is private", this, false);
                    return;
                }

                foreach (var game in des.response.games)
                {
                    if (game.appid == 252490)
                    {
                        double hours = game.playtime_forever / 60d;

                        if (connecting) Puts(lang.GetMessage("steam_connected", this, null), bp, Math.Round(hours, 2));

                        if (hours >= steamInGameTime)
                        {
                            Puts(lang.GetMessage("steam_marking", this, null), bp.displayName);
                            LogToFile(this.Name, date + bp + " has " + hours + " in game", this, false);
                            if (!storedData.players.ContainsKey(bp.userID))
                            {
                                LogToFile(this.Name, date + "Adding new entry for " + bp, this, false);
                                storedData.players.Add(bp.userID, -50d);
                                SaveFile();
                                return;
                            }

                            LogToFile(this.Name, date + "Overwriting existing entry for " + bp, this, false);
                            storedData.players[bp.userID] = -50d;
                            SaveFile();
                            return;
                        }
                    }
                }
            }, this);
        }

        private void StartChecking()
        {
            timer.Every(frequency, () => {
                Check();
            });
        }

        private void SaveFile() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData);

        private string CheckLeft(int intsecs)
        {
            string output;
            float hours, mins, days;
            days = (float)intsecs / 86400;
            hours = (float)intsecs / 3600;
            mins = intsecs / 60;
            double q = Math.Round(hours, 1);
            double q2 = Math.Round(mins, 0);
            double q3 = Math.Round(days, 1);

            if (intsecs < 60)
            {
                output = intsecs + lang.GetMessage("secs", this, null);
                return output;
            }

            if (intsecs == 60)
            {
                output = q2 + lang.GetMessage("min", this, null);
                return output;
            }

            if (intsecs > 60 && intsecs < 3600)
            {
                output = q2 + lang.GetMessage("mins", this, null);
                return output;
            }

            if (intsecs == 3600)
            {
                output = q2 + lang.GetMessage("hour", this, null);
                return output;
            }

            if (intsecs > 3600 && intsecs < 86400)
            {
                output = q + lang.GetMessage("hours", this, null);
                return output;
            }

            if (intsecs == 86400)
            {
                output = q3 + lang.GetMessage("day", this, null);
                return output;
            }
            if (intsecs > 86400)
            {
                output = q3 + lang.GetMessage("days", this, null);
                return output;
            }

            return "";
        }

        private BaseEntity GetLookAtEntity(BasePlayer player, float maxDist = 250, int coll = -1)
        {
            if (player == null || player.IsDead()) return null;
            RaycastHit hit;
            var currentRot = Quaternion.Euler(player?.serverInput?.current?.aimAngles ?? Vector3.zero) * Vector3.forward;
            var ray = new Ray((player?.eyes?.position ?? Vector3.zero), currentRot);
            if (UnityEngine.Physics.Raycast(ray, out hit, maxDist, ((coll != -1) ? coll : layers)))
            {
                var ent = hit.GetEntity() ?? null;
                if (ent != null && !(ent?.IsDestroyed ?? true)) return ent;
            }
            return null;
        }

        private bool playerIsNew(ulong ID)
        {
            if (!storedData.players.ContainsKey(ID)) return false;
            if (storedData.players[ID] == -50d) return false;
            if (storedData.players[ID] < time || storedData.players[ID] == -25d) return true;
            return false;
        }

        #endregion
        #region Commands

        [ConsoleCommand("antinoob.addnoob")]
        private void AddCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            if (arg.Args == null || arg == null || arg.cmd == null || arg?.Args?.Length == 0 || arg.FullString == null)
            {
                Puts(lang.GetMessage("console_newhelp", this, null));
                return;
            }

            ulong ID_u;

            if (!ulong.TryParse(arg.Args[0], out ID_u))
            {
                Puts(lang.GetMessage("console_wrongSteamID", this, null));
                return;
            }

            foreach (var entry in storedData.players)
            {
                if (entry.Key == ID_u)
                {
                    if (storedData.players[entry.Key] == -25d)
                    {
                        Puts(lang.GetMessage("console_alreadynew", this, null));
                        return;
                    }

                    storedData.players[entry.Key] = -25d;
                    Puts(lang.GetMessage("console_setnew", this, null), ID_u);
                    SaveFile();
                    return;
                }
            }
            Puts(lang.GetMessage("console_cantfind", this, null));
        }

        [ConsoleCommand("antinoob.removenoob")]
        private void RemoveCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            if (arg.Args == null || arg == null || arg.cmd == null || arg.Args.Length == 0 || arg.FullString == null)
            {
                Puts(lang.GetMessage("console_removenewhelp", this, null));
                return;
            }

            ulong ID_u;

            if (!ulong.TryParse(arg.Args[0], out ID_u))
            {
                Puts(lang.GetMessage("console_wrongSteamID", this, null));
                return;
            }

            foreach (var entry in storedData.players)
            {
                if (entry.Key == ID_u)
                {
                    if (storedData.players[entry.Key] == -50d)
                    {
                        Puts(lang.GetMessage("console_alreadynot", this, null));
                        return;
                    }

                    storedData.players[entry.Key] = -50d;
                    Puts(lang.GetMessage("console_setnot", this, null), ID_u);
                    SaveFile();
                    return;
                }
            }
            Puts(lang.GetMessage("console_cantfind", this, null));
        }

        [ConsoleCommand("antinoob.checksteam")]
        private void checkSteamCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;

            if (string.IsNullOrEmpty(apiKey))
            {
                Puts(lang.GetMessage("steam_wrongapikey", this, null));
                return;
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList) SteamCheck(player, false);
        }

        [ConsoleCommand("antinoob.wipe.playerdata")]
        private void wipeDataCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.players.Clear();
            Puts(lang.GetMessage("dataFileWiped_playerdata", this, null));
            SaveFile();
        }

        [ConsoleCommand("antinoob.wipe.attempts")]
        private void wipeAttCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.AttackAttempts.Clear();
            Puts(lang.GetMessage("dataFileWiped_attempts", this, null));
            SaveFile();
        }

        [ConsoleCommand("antinoob.wipe.all")]
        private void wipeAllCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.AttackAttempts.Clear();
            storedData.ItemList.Clear();
            storedData.players.Clear();
            Puts(lang.GetMessage("dataFileWiped", this, null));
            SaveFile();
        }

        [ChatCommand("docheck")]
        private void docheckCmd(BasePlayer player, string command, string[] args)
        {
            if (!debug || !player.IsAdmin) return;

            if (args.Length < 1) return;
            string ID = args[0];
            ulong r = -0u;
            ulong.TryParse(ID, out r);
            if (r == -0u) return;
            double time = -1d;

            try
            {
                time = PlaytimeTracker?.Call<double>("GetPlayTime", ID) ?? -1d;
            }

            catch (Exception exc)
            {
                Puts(lang.GetMessage("userinfo_nofoundmsg", this, null), ID);
                storedData.playersWithNoData.Add(ulong.Parse(ID));
                SaveFile();
            }

            if (time == -1d)
            {
                Puts(lang.GetMessage("pt_notInstalled", this, null));
                return;
            }
        }

        [ChatCommand("testapi")]
        private void apiTest(BasePlayer player, string command, string[] args)
        {
            if (!debug || !player.IsAdmin) return;

            if (args.Length < 1)
            {
                SendReply(player, "/testapi <steamID>");
                return;
            }

            double time = -1d;
            string ID = args[0];
            ulong d;

            if (!ulong.TryParse(ID, out d))
            {
                SendReply(player, "Wrong ID");
                return;
            }

            webrequest.Enqueue("http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key=" + apiKey + "&steamid=" + ID, null, (code, response) =>
            {
                if (code != 200)
                {
                    Puts("Failed to contact steam API, profile is private/wrong API key (" + code + ")");
                    return;
                }

                var des = Deserialise<steamGames>(response);
                if (des.response.games == null)
                {
                    SendReply(player, "Steam profile of " + d + " is private");
                    return;
                }

                foreach (var game in des.response.games)
                {
                    if (game.appid == 252490)
                    {
                        double hours = game.playtime_forever / 60d;
                        hours = Math.Round(hours, 2);
                        SendReply(player, "Got hours for " + ID + " : " + hours);
                        if (hours >= steamInGameTime)
                        {
                            SendReply(player, "Marking " + ID + " as non noob");
                            if (!storedData.players.ContainsKey(d))
                            {
                                storedData.players.Add(d, -50d);
                                SaveFile();
                                return;
                            }

                            storedData.players[d] = -50d;
                            SaveFile();
                            return;
                        }
                    }
                }
                SendReply(player, "Steam profile of " + d + " is private");
                return;
            }, this);

            try
            {
                time = PlaytimeTracker?.Call<double>("GetPlayTime", ID) ?? -1d;
                if (time == -1d)
                {
                    SendReply(player, "Playtime tracker is not installed!");
                    return;
                }
                SendReply(player, "Time: " + time);
            }

            catch (Exception ex)
            {
                //Shows info about error
                var t = ex.Message;
                SendReply(player, t + " (NOT FOUND)");
            }

        }

        [ChatCommand("entdebug")]
        private void EntDebugCmd(BasePlayer player, string command, string[] args)
        {
            if (!debug || !player.IsAdmin) return;

            BaseEntity ent = GetLookAtEntity(player, 20f, layers);

            if (ent == null) return;
            var own = ent.OwnerID;

            if (args.Length < 1)
            {
                SendReply(player, "OwnerID: " + own);
                return;
            }

            var t = ulong.Parse(args[0]);
            ent.OwnerID = t;
            ent.SendNetworkUpdate();

            SendReply(player, "Set OwnerID: " + ent.OwnerID);
            return;
        }

        [ChatCommand("refunditem")]
        private void refundCmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "antinoobraid.admin"))
            {
                SendReply(player, lang.GetMessage("refunditem_noperm", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, lang.GetMessage("refunditem_help", this, player.UserIDString));
                return;
            }


            Item helditem = player.GetActiveItem();

            switch (args[0].ToLower())
            {
                case "add":
                    {
                        if (player.GetActiveItem() == null)
                        {
                            SendReply(player, lang.GetMessage("refunditem_needholditem", this, player.UserIDString));
                            return;
                        }

                        if (raidtools.ContainsKey(helditem.info.shortname))
                        {
                            if (!storedData.ItemList.ContainsKey(helditem.info.shortname))
                            {
                                storedData.ItemList.Add(helditem.info.shortname, raidtools[helditem.info.shortname]);
                                SaveFile();
                                SendReply(player, lang.GetMessage("refunditem_added", this, player.UserIDString), helditem.info.displayName.english);
                                return;
                            }

                            SendReply(player, lang.GetMessage("refunditem_alreadyonlist", this, player.UserIDString));
                            return;
                        }

                        SendReply(player, lang.GetMessage("refunditem_notexplosive", this, player.UserIDString));
                        return;
                    }

                case "remove":
                    {
                        if (player.GetActiveItem() == null)
                        {
                            SendReply(player, lang.GetMessage("refunditem_added", this, player.UserIDString), helditem.info.displayName.english);
                            return;
                        }

                        if (storedData.ItemList.ContainsKey(helditem.info.shortname))
                        {
                            storedData.ItemList.Remove(helditem.info.shortname);
                            SaveFile();
                            SendReply(player, lang.GetMessage("refunditem_removed", this, player.UserIDString), helditem.info.displayName.english);
                            return;
                        }

                        SendReply(player, lang.GetMessage("refunditem_notonlist", this, player.UserIDString));
                        return;
                    }

                case "all":
                    {
                        foreach (var t in raidtools) if (!storedData.ItemList.ContainsKey(t.Key)) storedData.ItemList.Add(t.Key, t.Value);
                        SendReply(player, lang.GetMessage("refunditem_addedall", this, player.UserIDString));
                        SaveFile();
                        return;
                    }

                case "clear":
                    {
                        storedData.ItemList.Clear();
                        SaveFile();
                        SendReply(player, lang.GetMessage("refunditem_cleared", this, player.UserIDString));
                        return;
                    }

                case "list":
                    {
                        if (storedData.ItemList.Count < 1)
                        {
                            SendReply(player, lang.GetMessage("refunditem_empty", this, player.UserIDString));
                            return;
                        }

                        List<string> T2 = new List<string>();

                        foreach (var entry in storedData.ItemList)
                        {
                            Item item = ItemManager.CreateByName(entry.Key, 1);

                            if (item.info.displayName.english == null)
                            {
                                LogToFile(this.Name, "Failed to find display name for " + entry.Key, this, true);
                            }

                            T2.Add(item?.info?.displayName?.english);
                        }

                        string final = string.Join("\n", T2.ToArray());
                        SendReply(player, lang.GetMessage("refunditem_list", this, player.UserIDString), final);
                        return;
                    }

                default:
                    {
                        SendReply(player, lang.GetMessage("refunditem_help", this, player.UserIDString));
                        return;
                    }
            }
        }

        [ChatCommand("checknew")]
        private void checkNewCmd(BasePlayer player, string command, string[] args)
        {
            BaseEntity hitEnt = GetLookAtEntity(player, 20f, layers);

            if (hitEnt == null)
            {
                SendReply(player, lang.GetMessage("NotLooking", this, player.UserIDString));
                return;
            }

            if (hitEnt.OwnerID == 0)
            {
                //DEBUGGING
                Puts("Couldn't find owner for entity at " + hitEnt.transform.position + " , on request by player " + player.userID);
                LogToFile(this.Name, "Couldn't find owner for entity at " + hitEnt.transform.position + " on request by player " + player.userID, this, true);
                return;
            }

            if (!storedData.players.ContainsKey(hitEnt.OwnerID))
            {
                Puts("No info found for player " + hitEnt.OwnerID + " at " + hitEnt.transform.position + " report this on Oxidemod! (checknew) (" + hitEnt.ShortPrefabName + ")");
                return;
            }

            double ftime = storedData.players[hitEnt.OwnerID];

            int left = (int)(time - ftime);

            if (show)
            {
                if (playerIsNew(hitEnt.OwnerID))
                {
                    if (showTime)
                    {
                        SendReply(player, lang.GetMessage("cannot_attack_time", this, player.UserIDString), CheckLeft(left));
                        return;
                    }

                    SendReply(player, lang.GetMessage("cannot_attack_no_time", this, player.UserIDString));
                    return;
                }
                SendReply(player, lang.GetMessage("can_attack", this, player.UserIDString));
            }
        }
        #endregion
    }
}