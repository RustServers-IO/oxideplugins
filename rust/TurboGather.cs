using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.Globalization;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("TurboGather", "redBDGR", "1.0.7", ResourceId = 2221)]
    [Description("Lets players activate a resouce gather boost for a certain amount of time")]

    class TurboGather : RustPlugin
    {
        #region Init / Config / Data

        #region Data

        //
        // Shoutouts to k1lly0u for help with the database stuff
        //

        private DynamicConfigFile turboGatherData;
        StoredData storedData;

        Dictionary<string, Information> cacheDictionary = new Dictionary<string, Information>();
        List<string> Animals = new List<string>();

        class StoredData
        {
            public Dictionary<string, Information> turboGatherInformation = new Dictionary<string, Information>();
        }

        class Information
        {
            public bool turboEnabled;
            public double activeAgain;
            public double turboEndTime;
            public bool adminTurboGiven;
            public double adminMultiplierGiven;
        }

        void SaveData()
        {
            storedData.turboGatherInformation = cacheDictionary;
            turboGatherData.WriteObject(storedData);
        }

        void LoadData()
        {
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title);
                cacheDictionary = storedData.turboGatherInformation;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }
        #endregion

        protected override void LoadDefaultConfig()
        {
            Config["boostMultiplier"] = boostMultiplier = GetConfig("boostMultiplier", 1.0);
            Config["activeTime"] = activeTime = GetConfig("activeTime", 30.0f);
            Config["cooldownTime"] = cooldownTime = GetConfig("cooldownTime", 600.0);

            Config["boostMultiplierVIP"] = boostMultiplierVIP = GetConfig("boostMultiplierVIP", 1.0);
            Config["activeTimeVIP"] = activeTimeVIP = GetConfig("activeTimeVIP", 30.0f);
            Config["cooldownTimeVIP"] = cooldownTimeVIP = GetConfig("cooldownTimeVIP", 600.0);

            Config["boostMultiplierANIMAL"] = boostMultiplierANIMAL = GetConfig("boostMultiplierVIP", 1.0);
            Config["activeTimeANIMAL"] = activeTimeANIMAL = GetConfig("activeTimeVIP", 30.0f);

            Config["dispenserEnabled"] = dispenserEnabled = GetConfig("dispenserEnabled", true);
            Config["pickupEnabled"] = pickupEnabled = GetConfig("pickupEnabled", true);
            Config["quarryEnabled"] = quarryEnabled = GetConfig("quarryEnabled", true);

            Config["activateTurboOnPlayerKill"] = activateTurboOnPlayerKill = GetConfig("activateTurboOnPlayerKill", false);
            Config["activateTurboOnFriendlyAnimalKill"] = activateTurboOnFriendlyAnimalKill = GetConfig("activateTurboOnFriendlyAnimalKill", false);
            Config["activateTurboOnHostileAnimalKill"] = activateTurboOnHostileAnimalKill = GetConfig("activateTurboOnHostileAnimalKill", false);

            Config.Save();
        }

        double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        public float activeTime = 30.0f;
        public double cooldownTime = 600.0;
        public double boostMultiplier = 1.0;

        public double endTime;

        public const string permissionName = "turbogather.use";
        public const string permissionNameVIP = "turbogather.vip";
        public const string permissionNameANIMAL = "turbogather.animal";
        public const string permissionNameADMIN = "turbogather.admin";

        public float activeTimeVIP = 30.0f;
        public double cooldownTimeVIP = 600.0;
        public double boostMultiplierVIP = 1.0;

        public float activeTimeANIMAL = 30.0f;
        public double boostMultiplierANIMAL = 1.0;

        public bool dispenserEnabled = true;
        public bool pickupEnabled = true;
        public bool quarryEnabled = true;

        public bool activateTurboOnPlayerKill = false;
        public bool activateTurboOnFriendlyAnimalKill = false;
        public bool activateTurboOnHostileAnimalKill = false;

        void Loaded()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BoostStart"] = "<color=#00FFFF>Engaging turbo gather! (x{0} resources for {1}s) </color>",
                ["BoostEnd"] = "<color=#00FFFF>Your TurboBoost has ended! (available again in {0}s) </color>",
                ["NoPermissions"] = "<color=#B20000>You do not have the required permissions to use this command! </color>",
                ["AlreadyInUse"] = "<color=#B20000>Your ability is already in use!</color>",
                ["OnCooldown"] = "<color=#B20000>You are currently on cooldown! ({0}s remaining) </color>",
                ["CooldownEnded"] = "<color=#00FFFF>Your cooldown has ended! </color>",
                ["AdminInvalidSyntax"] = "<color=#B20000>Invalid syntax! /giveturbo <playername> <length> <multiplier> </color>",
                ["PlayerOffline"] = "<color=#B20000>The playername / ID you entered is not online or invalid! </color>",
                ["PlayerGivenTurbo"] = "<color=#00FFFF>{0}'s TurboGather has been activated! (x{1} for {2}s) </color>",
                ["AdminBoostEnd"] = "<color=#00FFFF>Your admin applied TurboBoost has ended! </color>",
                ["AdminBoostStart"] = "<color=#00FFFF>An admin has given you turbo gather! (x{0} resources for {1}s) </color>",
                ["AnimalBoostEnd"] = "<color=#00FFFF>Your turbo gather has ended!</color>",

            }, this);

            turboGatherData = Interface.Oxide.DataFileSystem.GetFile("TurboGather");
        }

        void Unload()
        {
            SaveData();
        }

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permissionName, this);
            permission.RegisterPermission(permissionNameVIP, this);
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameANIMAL, this);
            LoadData();

            // adding animals to the animals list depending on what options are set in the config
            // this is the best method until i get time to sort out something better (sorry)
            if (activateTurboOnPlayerKill == true)
                Animals.Add("player");
            if (activateTurboOnFriendlyAnimalKill == true)
                Animals.Add("boar"); Animals.Add("stag"); Animals.Add("chicken"); Animals.Add("horse");
            if (activateTurboOnHostileAnimalKill == true)
                Animals.Add("bear"); Animals.Add("wolf");
        }


        void OnServerSave()
        {
            SaveData();
        }

        #endregion

        //
        // Shoutouts to Nogrod for the snippet below taken from PrivateMessage ( http://oxidemod.org/plugins/private-messaging.659/ )
        //

        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrId)
                    return activePlayer;
            }
            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator == null || !(info.Initiator is BasePlayer || !entity.isActiveAndEnabled)) return;
            if (permission.UserHasPermission(info.InitiatorPlayer.UserIDString, permissionNameANIMAL))
            {
                if (cacheDictionary.ContainsKey(info.InitiatorPlayer.UserIDString))
                {
                    if (entity.ShortPrefabName == "player" || entity.ShortPrefabName == "boar" || entity.ShortPrefabName == "stag" || entity.ShortPrefabName == "horse" || entity.ShortPrefabName == "chicken" || entity.ShortPrefabName == "bear" || entity.ShortPrefabName == "wolf")
                    {
                        if (Animals.Contains(entity.ShortPrefabName))
                        {
                            BasePlayer player = info.InitiatorPlayer;
                            StartAnimalTurbo(player);
                        }
                    }
                }
                else
                {
                    if (entity.ShortPrefabName == "player" || entity.ShortPrefabName == "boar" || entity.ShortPrefabName == "stag" || entity.ShortPrefabName == "horse" || entity.ShortPrefabName == "chicken" || entity.ShortPrefabName == "bear" || entity.ShortPrefabName == "wolf")
                    {
                        if (Animals.Contains(entity.ShortPrefabName))
                        {
                            cacheDictionary.Add(info.InitiatorPlayer.UserIDString, new Information { turboEnabled = false, activeAgain = GrabCurrentTime(), turboEndTime = 0 });
                            StartAnimalTurbo(info.InitiatorPlayer);
                        }
                    }
                }
            }
        }

        #region Dispenser & Pickups

        void DoGather(BasePlayer player, Item item)
        {
            if (player == null)
                return;
            if (cacheDictionary.ContainsKey(player.UserIDString))
            {
                if (cacheDictionary[player.UserIDString].adminTurboGiven == true)
                {
                    if (cacheDictionary[player.UserIDString].turboEndTime > GrabCurrentTime())
                        item.amount = (int)(item.amount * cacheDictionary[player.UserIDString].adminMultiplierGiven);
                    else if (cacheDictionary[player.UserIDString].turboEndTime < GrabCurrentTime())
                        cacheDictionary[player.UserIDString].adminTurboGiven = false;
                }
                else
                {
                    if (permission.UserHasPermission(player.UserIDString, permissionName) || permission.UserHasPermission(player.UserIDString, permissionNameVIP))
                    {
                        if (cacheDictionary[player.UserIDString].turboEnabled == true)
                        {
                            if (cacheDictionary[player.UserIDString].turboEndTime > GrabCurrentTime())
                            {
                                if (permission.UserHasPermission(player.UserIDString, permissionName))
                                    item.amount = (int)(item.amount * boostMultiplier);
                                else if (permission.UserHasPermission(player.UserIDString, permissionNameVIP))
                                    item.amount = (int)(item.amount * boostMultiplierVIP);
                            }
                            else if (cacheDictionary[player.UserIDString].turboEndTime < GrabCurrentTime())
                                cacheDictionary[player.UserIDString].turboEnabled = false;
                        }
                    }
                }
            }
        }



        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenserEnabled == true)
            {
                BasePlayer player = entity.ToPlayer();
                string userIdentity = player.UserIDString;
                DoGather(entity.ToPlayer(), item);
            }
        }

        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (pickupEnabled == true)
                DoGather(player, item);
        }

        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (quarryEnabled == true)
            {
                BasePlayer player = BasePlayer.FindByID(quarry.OwnerID) ?? BasePlayer.FindSleeping(quarry.OwnerID);
                DoGather(player, item);
            }
        }

        #endregion

        #region Command
        void StartAnimalTurbo(BasePlayer player)
        {
            if (cacheDictionary.ContainsKey(player.UserIDString))
            {
                endTime = GrabCurrentTime() + activeTimeANIMAL;

                cacheDictionary[player.UserIDString].turboEnabled = true;
                cacheDictionary[player.UserIDString].activeAgain = endTime;
                cacheDictionary[player.UserIDString].turboEndTime = GrabCurrentTime() + activeTimeANIMAL;

                SendReply(player, string.Format(msg("BoostStart", player.UserIDString), boostMultiplierANIMAL, activeTimeANIMAL));

                timer.Once(activeTimeANIMAL, () =>
                {
                    if (player == null) return;
                    cacheDictionary[player.UserIDString].turboEnabled = false;
                    SendReply(player, string.Format(msg("AnimalBoostEnd", player.UserIDString)));
                });
            }
            else return;
        }

        void StartTurbo(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permissionNameVIP))
                endTime = GrabCurrentTime() + cooldownTimeVIP + activeTimeVIP;
            else
                endTime = GrabCurrentTime() + cooldownTime + activeTime;

            cacheDictionary[player.UserIDString].turboEnabled = true;
            cacheDictionary[player.UserIDString].activeAgain = endTime;
            cacheDictionary[player.UserIDString].turboEndTime = GrabCurrentTime() + activeTime;

            if (permission.UserHasPermission(player.UserIDString, permissionNameVIP))
            {
                SendReply(player, string.Format(msg("BoostStart", player.UserIDString), boostMultiplierVIP, activeTimeVIP));

                timer.Once(activeTimeVIP, () =>
                {
                    if (player == null) return;
                    cacheDictionary[player.UserIDString].turboEnabled = false;
                    SendReply(player, string.Format(msg("BoostEnd", player.UserIDString), cooldownTimeVIP));
                    float cooldownFloat = Convert.ToSingle(cooldownTimeVIP);

                    timer.Once(cooldownFloat, () =>
                    {
                        if (player == null) return;
                        SendReply(player, string.Format(msg("CooldownEnded", player.UserIDString)));
                    });
                });
            }
            else
            {
                SendReply(player, string.Format(msg("BoostStart", player.UserIDString), boostMultiplier, activeTime));

                timer.Once(activeTime, () =>
                {
                    if (player == null) return;
                    cacheDictionary[player.UserIDString].turboEnabled = false;
                    SendReply(player, string.Format(msg("BoostEnd", player.UserIDString), cooldownTime));
                    float cooldownFloat = Convert.ToSingle(cooldownTime);

                    timer.Once(cooldownFloat, () =>
                    {
                        if (player == null) return;
                        SendReply(player, string.Format(msg("CooldownEnded", player.UserIDString)));
                    });
                });
            }
        }

        [ChatCommand("turbo")]
        void turboCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                if (!permission.UserHasPermission(player.UserIDString, permissionNameVIP))
                {
                    SendReply(player, string.Format(msg("NoPermissions", player.UserIDString)));
                    return;
                }
                else if (permission.UserHasPermission(player.UserIDString, permissionNameVIP)) { }
            }

            if (cacheDictionary.ContainsKey(player.UserIDString))
            {
                if (GrabCurrentTime() > cacheDictionary[player.UserIDString].activeAgain)
                {
                    if (cacheDictionary[player.UserIDString].turboEnabled == false)
                        StartTurbo(player);

                    else if (cacheDictionary[player.UserIDString].turboEnabled == true)
                    {
                        cacheDictionary[player.UserIDString].turboEnabled = false;
                        StartTurbo(player);
                    }
                }
                else if (cacheDictionary[player.UserIDString].turboEnabled == true)
                    SendReply(player, string.Format(msg("AlreadyInUse", player.UserIDString)));
                else
                {
                    double cooldownTimeLeft = cacheDictionary[player.UserIDString].activeAgain - GrabCurrentTime();
                    SendReply(player, string.Format(msg("OnCooldown", player.UserIDString), (int)cooldownTimeLeft));
                }
            }
            else if (!cacheDictionary.ContainsKey(player.UserIDString))
            {
                cacheDictionary.Add(player.UserIDString, new Information { turboEnabled = false, activeAgain = GrabCurrentTime(), turboEndTime = 0 });
                StartTurbo(player);
            }
        }

        [ChatCommand("giveturbo")]
        void giveturboCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                SendReply(player, string.Format(msg("NoPermissions", player.UserIDString)));
                return;
            }

            if (args.Length == 3)
            {
                string playerNameInput = args[0];
                var playerNameID = FindPlayer(playerNameInput);
                string dictionaryPlayerName = playerNameID.UserIDString;
                float playerActiveLengthInput = float.Parse(args[1]);
                double playerMultiplierInput = Convert.ToDouble(args[2]);

                if (playerNameID != null)
                {
                    if (!cacheDictionary.ContainsKey(dictionaryPlayerName))
                        cacheDictionary.Add(player.UserIDString, new Information { turboEnabled = false, activeAgain = GrabCurrentTime(), turboEndTime = 0, adminTurboGiven = false, adminMultiplierGiven = 0 });

                    cacheDictionary[dictionaryPlayerName].turboEndTime = GrabCurrentTime() + playerActiveLengthInput;
                    cacheDictionary[dictionaryPlayerName].adminTurboGiven = true;
                    cacheDictionary[dictionaryPlayerName].adminMultiplierGiven = playerMultiplierInput;
                    SendReply(player, string.Format(msg("PlayerGivenTurbo", player.UserIDString), dictionaryPlayerName, playerMultiplierInput, playerActiveLengthInput));
                    playerNameID.ChatMessage(string.Format(msg("AdminBoostStart", playerNameInput), playerMultiplierInput, playerActiveLengthInput));

                    timer.Once(playerActiveLengthInput, () =>
                    {
                        if (playerNameID == null) return;
                        playerNameID.ChatMessage(string.Format(msg("AdminBoostEnd", playerNameInput)));
                    });
                }
                else
                {
                    SendReply(player, string.Format(msg("PlayerOffline", player.UserIDString)));
                    return;
                }
            }
            else
                SendReply(player, string.Format(msg("AdminInvalidSyntax")));
        }

        #endregion

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
