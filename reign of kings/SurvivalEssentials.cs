// Reference: MySql.Data

using System;
using System.Collections.Generic;
using CodeHatch.Build;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Core.Networking;
using CodeHatch.Blocks;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Entities.Players;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Networking.Events.Social;
using System.Data;
using System.Text;
using CodeHatch.Cache;
using CodeHatch.Inventory;
using CodeHatch.Inventory.Equipment;
using CodeHatch.Inventory.Blueprints;
using CodeHatch.Inventory.Blueprints.Components;
using CodeHatch.ItemContainer;
using CodeHatch.Permissions;
using UnityEngine;
using CodeHatch;
using CodeHatch.Common;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Thrones.Tax;
using CodeHatch.Thrones.AncientThrone;
using System.Reflection;
using System.Linq;

using MySql.Data;
using MySql.Data.MySqlClient;
using MySql.Data.Common;

using CodeHatch.Engine.Modules.Inventory.Resource;

namespace Oxide.Plugins
{
    public static class ChatColours
    {
        public static string Red = "FF0000";
        public static string Blue = "0066CC";
        public static string Yellow = "FFFF00";
        public static string Green = "006600";
        public static string Black = "000000";
        public static string White = "-";
    }

    class AmpCache
    {
        public ulong Id;
        public int CurrencyAmount;
        public bool HasSpawnProtection;
        public int SpawnProtectionSecondsLeft;
        public DateTime SpawnProtectionStarted;
        public bool IsDueling;
        public bool HasDuelOffer;
        public Player DuelOfferedBy;
        public DateTime OfferedToDuelAt;
        public int DuelPrize;
        public Player DuelingWith;
        public int BlocksPlaced;
        public int Warnings;
        public int PlayerKills;

        public AmpCache(ulong pId, int pCurrencyAmount, bool pIsProtected, int pTimeLeft)
        {
            Id = pId;
            CurrencyAmount = pCurrencyAmount;
            SpawnProtectionSecondsLeft = pTimeLeft;
            HasSpawnProtection = pIsProtected;

            IsDueling = false;
            HasDuelOffer = false;
            DuelOfferedBy = null;
            OfferedToDuelAt = DateTime.Now;
            DuelPrize = 0;
            DuelingWith = null;

            if (SpawnProtectionSecondsLeft > 0)
            {
                SpawnProtectionStarted = DateTime.Now;
            }

            BlocksPlaced = 0;
            Warnings = 0;
            PlayerKills = 0;
        }
    }

    class AmpMarketplace
    {
        public Dictionary<int, InvItemBlueprint> Items;
        public Dictionary<int, int> ItemBuyPrices;
        public Dictionary<int, int> ItemSellPrices;
        public Dictionary<int, int> ItemAmounts;

        public AmpMarketplace(MySqlConnection Database)
        {
            Items = new Dictionary<int, InvItemBlueprint>();
            ItemBuyPrices = new Dictionary<int, int>();
            ItemSellPrices = new Dictionary<int, int>();
            ItemAmounts = new Dictionary<int, int>();

            RefreshMarketplace(Database);
        }

        public void UpdateItemPrice(int ItemId, int NewPrice, string Type = "buy")
        {
            switch (Type.ToLower())
            {
                case "buy":
                    {
                        ItemBuyPrices[ItemId] = NewPrice;
                        break;
                    }
                case "sell":
                    {
                        ItemSellPrices[ItemId] = NewPrice;
                        break;
                    }
            }
        }

        public void UpdateItemCount(int ItemId, int NewCount, MySqlConnection Database)
        {
            string Query = "UPDATE marketplace SET amount = '" + NewCount.ToString() + "' WHERE id = '" + ItemId.ToString() + "'";
            Database.Open();
            MySqlCommand Command = new MySqlCommand(Query, Database);
            Command.ExecuteNonQuery();
            Command.Dispose();
            Database.Close();

            ItemAmounts[ItemId] = NewCount;
        }

        public void RefreshMarketplace(MySqlConnection Database)
        {
            Items.Clear();
            ItemBuyPrices.Clear();
            ItemSellPrices.Clear();
            ItemAmounts.Clear();

            int ItemId = 1;

            Database.Open();
            string Query = "SELECT name,price_buy,price_sell,amount,id FROM marketplace ORDER BY id ASC";
            MySqlCommand Command = new MySqlCommand(Query, Database);
            MySqlDataReader Reader = Command.ExecuteReader();

            while (Reader.Read())
            {
                string ItemName = Reader.GetString(0);
                int BuyPrice = Reader.GetInt32(1);
                int SellPrice = Reader.GetInt32(2);
                int AmountLeft = Reader.GetInt32(3);

                InvItemBlueprint BlueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(ItemName, true, true);

                if (BlueprintForName != null)
                {
                    Items.Add(ItemId, BlueprintForName);
                    ItemBuyPrices.Add(ItemId, BuyPrice);
                    ItemSellPrices.Add(ItemId, SellPrice);
                    ItemAmounts.Add(ItemId, AmountLeft);
                }

                ItemId++;
            }

            Reader.Dispose();
            Command.Dispose();
            Database.Close();
        }
    }

    [Info("SurvivalEssentials", "Jonty", "2.2.1")]
    class SurvivalEssentials : ReignOfKingsPlugin
    {
        // Database Configuration
        string DatabaseUser = "root";
        string DatabasePassword = "lol123";
        string DatabaseHost = "127.0.0.1";
        string DatabaseName = "rok";
        string DatabasePort = "3306";

        // Currency Configuration
        string CurrencyName = "Gold";
        int DefaultAmount = 100;

        // Currency Timer Configuration
        int TimerResetInterval = 1800;
        int TimerCurrencyAmount = 150;

        // Announcement Timer Configuration
        int TimerAnnouncementInterval = 300;

        // Crest Configuration
        bool DisableDeCresting = true;

        // Bounties (non-DB driven)
        Dictionary<Player, int> Bounties;

        // Currency Shop
        Dictionary<string, int> CurrencyShopStrings;
        Dictionary<InvItemBlueprint, int> CurrencyShop;

        // Announcements
        Dictionary<string, string> Announcements;

        // No Build Zones
        Dictionary<Vector2, float> ProtectedZones = new Dictionary<Vector2, float>();

        // Crest Destroyable Zones
        Dictionary<Vector2, float> CrestZones = new Dictionary<Vector2, float>();

        // Administration
        List<ulong> Administrators;
        List<string> RuleList;

        // Cache
        Dictionary<Player, AmpCache> Cache;

        // Marketplace
        AmpMarketplace Marketplace;

        // DB stuffs
        string ConnectionString;

        static void Main() { }

        void LoadAdmins()
        {
            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Connection.Open();

                MySqlCommand CheckStatus = new MySqlCommand("SELECT steamid FROM admins", Connection);
                MySqlDataReader Reader = CheckStatus.ExecuteReader();

                if (Reader != null)
                {
                    while (Reader.Read())
                    {
                        ulong Admin = Reader.GetUInt64(0);

                        if (!Administrators.Contains(Admin))
                            Administrators.Add(Admin);
                    }
                }

                Reader.Dispose();
                CheckStatus.Dispose();
            }               
        }

        void SendMessageToAdmins(string Message, bool IsPrivate = false, Player Originator = null)
        {
            string Colour = ChatColours.Yellow;
            string Prefix = "[[" + ChatColours.Blue + "]Admin Chat[-]] ";

            if (!IsPrivate)
            {
                Prefix = "[[" + ChatColours.Blue + "]Admin Alert[-]]";
            }

            if (IsPrivate)
            {
                Prefix += Originator.DisplayName + " :";
            }


            foreach (ulong Admin in Administrators)
            {
                if (Server.PlayerIsOnline(Admin))
                {
                    PrintToChat(Server.GetPlayerById(Admin), "{0} {1}[-]", Prefix, Message);
                }
            }
        }

        void CacheAllOnlinePlayers()
        {
            if (Server.AllPlayers.Count > 0)
            {
                foreach (Player Player in Server.AllPlayers)
                {
                    if (Player.Name.ToLower() == "server")
                    {
                        continue;
                    }

                    if (Server.PlayerIsOnline(Player.DisplayName))
                    {
                        if (!Cache.ContainsKey(Player))
                        {
                            Cache.Add(Player, new AmpCache(Player.Id, 0, false, 0));
                            PopulateCache(Player);
                        }
                    }
                }
            }

            PrintWarning("{0} players added to cache.", Cache.Count);
        }

        void CheckSpawnProtection()
        {
            foreach (Player Player in Server.AllPlayers)
            {
                AmpCache CachedPlayer = Cache.TryGetValue(Player, out CachedPlayer) ? CachedPlayer : null;

                if (CachedPlayer != null && CachedPlayer.HasSpawnProtection)
                {
                    TimeSpan TimePassed = DateTime.Now - CachedPlayer.SpawnProtectionStarted;

                    if (TimePassed.Seconds >= CachedPlayer.SpawnProtectionSecondsLeft)
                    {
                        CachedPlayer.HasSpawnProtection = false;
                        CachedPlayer.SpawnProtectionSecondsLeft = 0;

                        UpdateDatabase(Player);
                    }
                    else
                    {
                        CachedPlayer.SpawnProtectionSecondsLeft -= TimePassed.Seconds;
                    }
                }
            }
        }

        void UpdateDatabase(Player Player)
        {
            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Connection.Open();
                AmpCache CachedPlayer = Cache.TryGetValue(Player, out CachedPlayer) ? CachedPlayer : null;
                string HasProtection = CachedPlayer.HasSpawnProtection ? "1" : "0";

                MySqlCommand UpdateCommand = new MySqlCommand("UPDATE players SET currency = '" + CachedPlayer.CurrencyAmount + "', has_protection = '" + HasProtection + "', protection_left = '" + CachedPlayer.SpawnProtectionSecondsLeft + "', warnings = '" + CachedPlayer.Warnings + "', kills = '" + CachedPlayer.PlayerKills + "' WHERE id = '" + Player.Id + "'", Connection);
                UpdateCommand.ExecuteNonQuery();
                UpdateCommand.Dispose();
            }
        }

        void PopulateCache(Player Player)
        {
            AmpCache CachedPlayer = Cache.TryGetValue(Player, out CachedPlayer) ? CachedPlayer : null;

            if (CachedPlayer != null)
            {
                Dictionary<string, int> Data = PlayerIntegers(Player);

                CachedPlayer.CurrencyAmount = Data["Currency"];
                CachedPlayer.HasSpawnProtection = Data["ProtectionLeft"] > 0 ? true : false;
                CachedPlayer.SpawnProtectionSecondsLeft = Data["ProtectionLeft"];
                CachedPlayer.Warnings = Data["Warnings"];
                CachedPlayer.PlayerKills = Data["PlayerKills"];

                if (CachedPlayer.CurrencyAmount < 0)
                    CachedPlayer.CurrencyAmount = 0;
            }
        }

        bool CheckAdminStatus(Player Player)
        {
            return Administrators.Contains(Player.Id);
        }

        Dictionary<string, int> PlayerIntegers(Player Player)
        {
            Dictionary<string, int> IntegerValues = new Dictionary<string, int>();

            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Connection.Open();

                MySqlCommand GetValues = new MySqlCommand("SELECT currency, warnings, protection_left, kills FROM players WHERE id = '" + Player.Id + "'", Connection);
                MySqlDataReader Reader = GetValues.ExecuteReader();

                while (Reader.Read())
                {
                    IntegerValues.Add("Currency", Reader.GetInt32(0));
                    IntegerValues.Add("Warnings", Reader.GetInt32(1));
                    IntegerValues.Add("ProtectionLeft", Reader.GetInt32(2));
                    IntegerValues.Add("PlayerKills", Reader.GetInt32(3));
                }

                Reader.Dispose();
                GetValues.Dispose();
            }

            return IntegerValues;
        }

        bool IsSpawnProtected(Player Player)
        {
            AmpCache PlayerCache = GetCache(Player);

            if (PlayerCache != null)
            {
                if (PlayerCache.HasSpawnProtection)
                    return true;
            }

            return false;
        }

        AmpCache GetCache(Player Player)
        {
            AmpCache CachedPlayer = null;
            return Cache.TryGetValue(Player, out CachedPlayer) ? CachedPlayer : null;
        }

        void LoadProtectionZones()
        {
            ProtectedZones.Clear();

            // ProtectedZones.Add(new Vector2(x, z), distance);
        }

        void LoadDeCrestableZones()
        {
            CrestZones.Clear();

            CrestZones.Add(new Vector2(-45.09f, 295.68f), 100f); // StormWall Throne area
        }

        void LoadConfigFromDatabase()
        {
            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Connection.Open();
                MySqlCommand Config = new MySqlCommand("SELECT disable_decresting, announce_interval, currency_interval, currency_interval_amount FROM plugin_settings", Connection);
                MySqlDataReader Reader = Config.ExecuteReader();

                while (Reader.Read())
                {
                    int CrestInt = Reader.GetInt32(0);

                    if (CrestInt == 1)
                        DisableDeCresting = true;
                    else
                        DisableDeCresting = false;

                    TimerAnnouncementInterval = Reader.GetInt32(1);
                    TimerResetInterval = Reader.GetInt32(2);
                    TimerCurrencyAmount = Reader.GetInt32(3);
                }

                Reader.Dispose();
                Config.Dispose();
            }
        }

        void OnServerInitialized()
        {
            LoadAnnouncements();
            LoadGuidelines();
            LoadProtectionZones();
            LoadDeCrestableZones();
            LoadConfigFromDatabase();
            LoadAdmins();

            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Marketplace = new AmpMarketplace(Connection);
            }

            PrintWarning(Announcements.Count + " Announcements loaded.");
            PrintWarning(RuleList.Count + " Rules loaded.");
            PrintWarning(ProtectedZones.Count + " Protected Zones loaded.");
            PrintWarning(CrestZones.Count + " DeCrestable Zones loaded.");
            PrintWarning(Marketplace.Items.Count + " Marketplace Items loaded.");
            PrintWarning("Configuration loaded from database.");

            timer.Every(TimerAnnouncementInterval, AnnounceInformation);
            timer.Every(TimerResetInterval, UpdateCurrency);
            timer.Every(30, CheckSpawnProtection);

            CacheAllOnlinePlayers();
        }

        void Loaded()
        {
            Bounties = new Dictionary<Player, int>();
            CurrencyShopStrings = new Dictionary<string, int>();
            CurrencyShop = new Dictionary<InvItemBlueprint, int>();
            Announcements = new Dictionary<string, string>();
            Administrators = new List<ulong>();
            RuleList = new List<string>();
            Cache = new Dictionary<Player, AmpCache>();

            ConnectionString = "server=" + DatabaseHost + ";port=" + DatabasePort + ";uid=" + DatabaseUser + ";pwd=" + DatabasePassword + ";database=" + DatabaseName + ";";
        }

        string GetChatColour(string Input)
        {
            string Output = ChatColours.White;

            switch (Input.ToLower())
            {
                case "white":
                    {
                        Output = ChatColours.White;
                        break;
                    }
                case "green":
                    {
                        Output = ChatColours.Green;
                        break;
                    }
                case "blue":
                    {
                        Output = ChatColours.Blue;
                        break;
                    }
                case "yellow":
                    {
                        Output = ChatColours.Yellow;
                        break;
                    }
                case "black":
                    {
                        Output = ChatColours.Black;
                        break;
                    }
                case "red":
                    {
                        Output = ChatColours.Red;
                        break;
                    }
                default:
                    {
                        Output = ChatColours.White;
                        break;
                    }
            }

            return Output;
        }

        void LoadAnnouncements()
        {
            Announcements.Clear();

            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Connection.Open();

                MySqlCommand Announcement = new MySqlCommand("SELECT message,colour FROM announcements", Connection);
                MySqlDataReader Reader = Announcement.ExecuteReader();

                while (Reader.Read())
                {
                    Announcements.Add(Reader.GetString(0), Reader.GetString(1));
                }

                Reader.Dispose();
                Announcement.Dispose();
            }

            Announcements.Add("This server is running Survival Essentials v2.2.1", "Yellow");
        }

        void LoadGuidelines()
        {
            RuleList.Clear();

            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Connection.Open();

                MySqlCommand Rule = new MySqlCommand("SELECT rule FROM rules", Connection);
                MySqlDataReader Reader = Rule.ExecuteReader();

                while (Reader.Read())
                {
                    RuleList.Add(Reader.GetString(0));
                }

                Reader.Dispose();
                Rule.Dispose();
            }
        }

        void AnnounceInformation()
        {
            int Count = Announcements.Count;
            System.Random Random = new System.Random();
            int Number = Random.Next(0, Count);

            string Message = Announcements.ElementAt(Number).Key;
            string Colour = GetChatColour(Announcements.ElementAt(Number).Value);

            PrintToChat("Server:[{0}] {1}[-]", Colour, Message);
        }

        void UpdateCurrency()
        {
            foreach (Player Player in Server.AllPlayers)
            {
                if (!Server.PlayerIsOnline(Player.DisplayName))
                    continue;

                if (Player.DisplayName.ToLower() == "server")
                    continue;

                AmpCache CachedPlayer = GetCache(Player);
                CachedPlayer.CurrencyAmount += TimerCurrencyAmount;

                PrintToChat(Player, "[" + ChatColours.Yellow + "]You have been given " + TimerCurrencyAmount + " " + CurrencyName + " for staying active.[-]");
            }
        }

        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            try
            {
                Player Damager = e.Damage.DamageSource.Owner;

                bool IsSleeper = string.IsNullOrEmpty(e.Entity.name.ToString()) && e.Entity.name.ToString().Contains("Player Sleeper") ? true : false;
                bool IsAdministrator = Administrators.Contains(Damager.Id) ? true : false;

                if (e.Entity.IsPlayer && Damager != null)
                {
                    AmpCache OtherPlayer = Cache.TryGetValue(e.Entity.Owner, out OtherPlayer) ? OtherPlayer : null;
                    AmpCache Me = Cache.TryGetValue(Damager, out Me) ? Me : null;

                    if (OtherPlayer.HasSpawnProtection)
                    {
                        if (Damager != e.Entity.Owner)
                        {
                            e.Cancel("Player has spawn protection");
                            e.Damage.Amount = 0f;
                            PrintToChat(Damager, "This player has spawn protection!");
                            return;
                        }
                    }
                    else if (Me.HasSpawnProtection)
                    {
                        e.Cancel("You have spawn protection");
                        e.Damage.Amount = 0f;
                        PrintToChat(Damager, "[" + ChatColours.Red + "]You cannot attack players whilst under spawn protection! Time left: {0} seconds.[-]", Me.SpawnProtectionSecondsLeft);
                    }
                }

                if (Damager != null && IsSleeper)
                {
                    if (e.Damage.Amount > 0 && IsSleeper && !IsAdministrator)
                    {
                        e.Cancel("No damage to sleepers!");
                        e.Damage.Amount = 0f;
                        PrintToChat(Damager, "[" + ChatColours.Red + "]Sleepers cannot be killed on this server![-]");
                        return;
                    }
                }
                else if (Damager != null && e.Entity.name.ToLower().Contains("crest") && !IsAdministrator && DisableDeCresting)
                {
                    foreach (Vector2 Val in CrestZones.Keys)
                    {
                        float Dis = Math.Abs(Vector2.Distance(Val, new Vector2(e.Entity.Position.x, e.Entity.Position.z)));

                        if (Dis > 200f)
                        {
                            e.Cancel();
                            e.Damage.Amount = 0f;
                            PrintToChat(Damager, "[" + ChatColours.Red + "]Crests cannot be destroyed on this server.[-]");
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Damaged by another source (e.g plague, dehydration, hunger, anything that doesn't have an attacker)
            }
        }

        private void OnCubePlacement(CubePlaceEvent Event)
        {
            if (ProtectedZones.Count > 0)
            {
                foreach (Vector2 mark in ProtectedZones.Keys)
                {
                    float distance = Math.Abs(Vector2.Distance(mark, new Vector2(Event.Entity.Position.x, Event.Entity.Position.z)));
                    if (distance <= ProtectedZones[mark] && !Administrators.Contains(Event.Sender.Id))
                    {
                        Event.Cancel("This area is protected by an Administrator!");
                        PrintToChat(Event.Entity.Owner, "This area is protected by an Administrator!");
                        return;
                    }
                }
            }

            AmpCache Me = GetCache(Event.Sender);

            if (Me != null)
            {
                Me.BlocksPlaced += 1;

                if (Me.BlocksPlaced >= 5)
                {
                    Me.BlocksPlaced = 0;
                    Me.CurrencyAmount += 1;
                }
            }
        }

        private void OnCubeTakeDamage(CubeDamageEvent Event)
        {
            if (ProtectedZones.Count > 0)
            {
                foreach (Vector2 mark in ProtectedZones.Keys)
                {
                    float distance = Math.Abs(Vector2.Distance(mark, new Vector2(Event.Entity.Position.x, Event.Entity.Position.z)));
                    if (distance <= ProtectedZones[mark] && !Administrators.Contains(Event.Sender.Id))
                    {
                        Event.Damage.Amount = 0f;
                        Event.Damage.ImpactDamage = 0f;
                        Event.Damage.MiscDamage = 0f;

                        Event.Cancel("This area is protected by an Administrator!");
                        PrintToChat(Event.Entity.Owner, "This area is protected by an Administrator!");
                        return;
                    }
                }
            }
        }

        private void OnPlayerDisconnected(Player player)
        {
            PrintToChat(player.DisplayName + " has logged out.");
            PrintWarning(player.DisplayName + " has disconnected");

            if (Cache.ContainsKey(player))
            {
                UpdateDatabase(player);

                AmpCache Player = GetCache(player);

                if (Player.IsDueling)
                {
                    AmpCache Dueling = GetCache(Player.DuelingWith);

                    Dueling.DuelingWith = null;
                    Dueling.IsDueling = false;
                    Dueling.CurrencyAmount += Dueling.DuelPrize;

                    PrintToChat(Player.DuelingWith, "Your duel partner has logged out. You have recieved your money back.");
                }

                Cache.Remove(player);
            }

            if (Administrators.Contains(player.Id))
                Administrators.Remove(player.Id);
        }

        private void OnPlayerConnected(Player player)
        {
            PrintToChat(player.DisplayName + " has joined the server.");

            ulong UserId = player.Id;
            string Query = "SELECT id FROM players WHERE id = '" + UserId + "'";

            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Connection.Open();

                MySqlCommand GetCurrencyCount = new MySqlCommand(Query, Connection);
                bool DoesUserHaveRecord = GetCurrencyCount.ExecuteScalar() != null ? true : false;
                GetCurrencyCount.Dispose();

                if (!DoesUserHaveRecord)
                {
                    string InsertQuery = "INSERT INTO players (id, currency, originalname, currentname, has_protection, protection_left, warnings, kills) VALUES ('" + UserId + "', '" + DefaultAmount + "', '" + player.OriginalName + "', '" + player.DisplayName + "', '1', '3600', '0', '0')";
                    MySqlCommand InsertRecord = new MySqlCommand(InsertQuery, Connection);
                    InsertRecord.ExecuteNonQuery();
                    InsertRecord.Dispose();

                    PrintWarning(player.DisplayName + " has connected for the first time");

                    SendMessageToAdmins("A new player with the name " + player.DisplayName + " has joined.");
                }
                else
                {
                    PrintWarning(player.DisplayName + " has connected");

                    MySqlCommand UpdateName = new MySqlCommand("UPDATE players SET currentname = '" + player.DisplayName + "' WHERE id = '" + player.Id + "'", Connection);
                    UpdateName.ExecuteNonQuery();
                    UpdateName.Dispose();
                }
            }

            if (!Cache.ContainsKey(player))
            {
                Cache.Add(player, new AmpCache(player.Id, 0, false, 0));
                PopulateCache(player);
            }

            timer.Once(20, () => player.ShowPopup("Welcome to the server", "[" + ChatColours.Yellow + "]The plugins have been updated![-]\r\n--------------\r\n- You can now sell items to the Marketplace\r\n- Items on the Marketplace now have an inventory.\r\n- Plugin has been recoded with optimization in mind!\r\n\r\n[" + ChatColours.Red + "]If you are new to this server, welcome! You have an hour of Spawn Protection to help you get started. If you need help with our commands, use /modhelp[-]"));
        }

        private void OnEntityDeath(EntityDeathEvent Event)
        {
            if (Event.Entity.IsPlayer)
            {
                AmpCache CachedPlayer = Cache.TryGetValue(Event.KillingDamage.DamageSource.Owner, out CachedPlayer) ? CachedPlayer : null;
                AmpCache DeadPlayer = GetCache(Event.Entity.Owner);

                SendMessageToAdmins(Event.Entity.Owner.DisplayName + " has been killed by " + Event.KillingDamage.DamageSource.Owner.DisplayName);

                if (Bounties.ContainsKey(Event.Entity.Owner))
                {
                    PrintToChat("{0} has killed {1} for {2} {3}", Event.KillingDamage.DamageSource.Owner.Entity.Owner.DisplayName, Event.Entity.Owner.DisplayName, Bounties[Event.Entity.Owner], CurrencyName);

                    using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
                    {
                        Connection.Open();

                        MySqlCommand UpdateUser = new MySqlCommand("UPDATE players SET currency = currency + " + Bounties[Event.Entity.Owner] + " WHERE id = " + Event.KillingDamage.DamageSource.Owner.Entity.OwnerId, Connection);
                        UpdateUser.ExecuteNonQuery();
                        UpdateUser.Dispose();
                    }

                    CachedPlayer.CurrencyAmount += Bounties[Event.Entity.Owner];

                    Bounties.Remove(Event.Entity.Owner);
                    return;
                }
                else if (CachedPlayer.IsDueling && DeadPlayer.IsDueling)
                {
                    if (CachedPlayer.DuelingWith == Event.Entity.Owner)
                    {
                        CachedPlayer.DuelingWith = null;
                        CachedPlayer.IsDueling = false;

                        DeadPlayer.IsDueling = false;
                        DeadPlayer.DuelingWith = null;

                        CachedPlayer.CurrencyAmount += Convert.ToInt32(Math.Round(Convert.ToDecimal(CachedPlayer.DuelPrize * 2), 0, MidpointRounding.ToEven));
                        UpdateDatabase(Event.KillingDamage.DamageSource.Owner);

                        DeadPlayer.DuelPrize = 0;

                        PrintToChat("{0} has won the duel against {1}, and earned {2} {3} from it!", Event.KillingDamage.DamageSource.Owner.DisplayName, Event.Entity.Owner.DisplayName, CachedPlayer.DuelPrize, CurrencyName);
                        CachedPlayer.DuelPrize = 0;
                    }
                }
                else
                {
                    if (CachedPlayer != null)
                    {
                        CachedPlayer.PlayerKills += 1;
                        PrintToChat("{0} has been killed", Event.Entity.Owner.DisplayName);
                    }
                    else
                    {
                        PrintToChat("{0} has died", Event.Entity.Owner.DisplayName);
                    }
                }
            }
            else
            {
                // animal, villager etc
            }
        }

        [ChatCommand("leaderboards")]
        private void Leaderboards(Player Player, string Command, string[] Args)
        {
            string Output = "[" + ChatColours.Red + "]Top 5 Killers[-]\r\n--------------\r\n";
            int i = 1;

            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
            {
                Connection.Open();

                MySqlCommand GetKillers = new MySqlCommand("SELECT id,kills,currentname FROM players WHERE kills > 0 AND currentname != 'Server' ORDER BY kills DESC LIMIT 5", Connection);
                MySqlDataReader Reader = GetKillers.ExecuteReader();

                if (Reader != null)
                {
                    while (Reader.Read())
                    {
                        Output += "#" + i.ToString() + " - " + Reader.GetString(2) + " with " + Reader.GetInt32(1).ToString() + " kills\r\n";
                        i++;
                    }
                }

                Reader.Dispose();
                GetKillers.Dispose();

                i = 1;

                Output += "[" + ChatColours.Yellow + "]\r\nTop 5 Richest[-]\r\n--------------\r\n";

                MySqlCommand GetRichest = new MySqlCommand("SELECT id,currency,currentname FROM players WHERE currency > 0 AND currentname != 'Server' ORDER BY currency DESC LIMIT 5", Connection);
                MySqlDataReader Reader2 = GetRichest.ExecuteReader();

                if (Reader2 != null)
                {
                    while (Reader2.Read())
                    {
                        Output += "#" + i.ToString() + " - " + Reader2.GetString(2) + " with " + Reader2.GetInt32(1).ToString() + " " + CurrencyName + "\r\n";
                        i++;
                    }
                }

                Reader2.Dispose();
                GetRichest.Dispose();
            }

            Player.ShowPopup("Leaderboards", Output);
        }

        [ChatCommand("makeadmin")]
        private void MakeAdmin(Player Player, string Command, string[] Args)
        {
            if (Player.DisplayName.ToLower() == "server")
            {
                Player Admin = Server.GetPlayerByName(MergeParams(Args, 0));

                if (Admin != null && Server.PlayerIsOnline(MergeParams(Args, 0)))
                {
                    try
                    {
                        using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
                        {
                            Connection.Open();

                            MySqlCommand UpdateAdmins = new MySqlCommand("INSERT INTO admins (steamid) VALUES ('" + Admin.Id + "')", Connection);
                            UpdateAdmins.ExecuteNonQuery();
                            UpdateAdmins.Dispose();

                            PrintWarning("Player has been made an Administrator.");
                        }
                    }
                    catch
                    {
                        PrintWarning("That player is most likely already an Administrator, is offline, or the SQL server is unavailable.");
                    }

                    if (!Administrators.Contains(Admin.Id))
                    {
                        Administrators.Add(Admin.Id);
                    }
                }
            }
        }

        [ChatCommand("declineduel")]
        private void DeclineDuel(Player Player, string Command, string[] Args)
        {
            AmpCache Me = GetCache(Player);

            if (Me.HasDuelOffer)
            {
                Me.HasDuelOffer = false;
                Me.DuelOfferedBy = null;
            }
        }

        [ChatCommand("acceptduel")]
        private void AcceptDuel(Player Player, string Command, string[] Args)
        {
            AmpCache Me = GetCache(Player);

            if (Me.HasDuelOffer && !Me.IsDueling)
            {
                AmpCache Them = GetCache(Me.DuelOfferedBy);

                if (Them.IsDueling)
                    return;

                if (Me.CurrencyAmount < Me.DuelPrize)
                {
                    Me.DuelOfferedBy = null;
                    Me.HasDuelOffer = false;

                    PrintToChat(Player, "You cannot afford this duel.");
                    return;
                }

                Them.DuelPrize = Me.DuelPrize;
                Them.DuelingWith = Player;
                Them.IsDueling = true;

                Them.CurrencyAmount -= Them.DuelPrize;
                Me.CurrencyAmount -= Me.DuelPrize;

                Me.DuelingWith = Me.DuelOfferedBy;
                Me.IsDueling = true;

                Me.DuelOfferedBy = null;
                Me.HasDuelOffer = false;

                UpdateDatabase(Player);
                UpdateDatabase(Me.DuelingWith);

                PrintToChat("[FF0000]{0} has accepted a duel from {1} for {2} {3}![-]", Player.DisplayName, Me.DuelingWith.DisplayName, Me.DuelPrize, CurrencyName);
            }
        }

        [ChatCommand("duel")]
        private void ChallengeDuel(Player Player, string Command, string[] Args)
        {
            if (Args.Length >= 2)
            {
                AmpCache Me = GetCache(Player);
                int ChallengeAmount = Convert.ToInt32(Args[0]);
                string OtherPlayerString = MergeParams(Args, 1);

                Player OtherPlayer = Server.GetPlayerByName(OtherPlayerString);

                if (OtherPlayer != null)
                {
                    AmpCache Them = GetCache(OtherPlayer);

                    if (Me == Them)
                        return;

                    if (Me.CurrencyAmount >= ChallengeAmount)
                    {
                        if (ChallengeAmount > 0 && ChallengeAmount < 10000)
                        {
                            if (Them.HasDuelOffer)
                            {
                                TimeSpan Time = DateTime.Now - Them.OfferedToDuelAt;

                                if (Time.Minutes >= 5)
                                {
                                    Them.HasDuelOffer = false;
                                }
                            }

                            if (!Them.HasDuelOffer)
                            {
                                if (!Me.IsDueling)
                                {
                                    PrintToChat("[FF0000]{0} has challenged {1} to a duel for {2} {3}![-]", Player.DisplayName, OtherPlayer.DisplayName, ChallengeAmount, CurrencyName);
                                    OtherPlayer.ShowPopup("Duel Challenge!", "You have been challenged to a duel. Type /declineduel or /acceptduel to proceed.");

                                    Them.HasDuelOffer = true;
                                    Them.OfferedToDuelAt = DateTime.Now;
                                    Them.DuelOfferedBy = Player;
                                    Them.DuelPrize = ChallengeAmount;
                                }
                                else
                                {
                                    PrintToChat("You already have a duel with {0} for {1} {2}!", Me.DuelOfferedBy, Me.DuelPrize, CurrencyName);
                                }
                            }
                            else
                            {
                                PrintToChat(Player, "{0} already has a duel offer.", OtherPlayer.DisplayName);
                            }
                        }
                        else
                        {
                            PrintToChat(Player, "Challenge amount must be above 0 and below 10000");
                        }
                    }
                    else
                    {
                        PrintToChat(Player, "You can't afford to gamble that much.");
                    }
                }
                else
                {
                    PrintToChat(Player, "That player is either not online, or does not exist.");
                }
            }
            else
            {
                PrintToChat(Player, "Usage: /duel <bet amount> <player>");
            }
        }

        [ChatCommand("rules")]
        private void Rules(Player Player, string Command, string[] Args)
        {
            int i = 1;
            string Output = "Server Rules\r\n\r\n";

            foreach (string Value in RuleList)
            {
                Output += i.ToString() + ". " + Value + "\r\n";
                i++;
            }

            Player.ShowPopup("Rule List", Output);
        }

        [ChatCommand("ip")]
        void GetIp(Player Player, string Command, string[] Args)
        {
            Player Playerr = Server.GetPlayerByName(Args[0]);

            PrintToChat(Player, "{0}'s IP address is {1}", Playerr.DisplayName, Playerr.Connection.ExternalIp);
        }

        [ChatCommand("admins")]
        private void Admins(Player Player, string Command, string[] Args)
        {
            int i = 0;
            string Output = "Online Administrators:\r\n\r\n";

            foreach (ulong Admin in Administrators)
            {
                if (Server.PlayerIsOnline(Admin))
                {
                    Output += "- " + Server.GetPlayerById(Admin).DisplayName + "\r\n";
                    i++;
                }
            }

            if (i > 0)
            {
                Player.ShowPopup("Online Staff", Output);
            }
            else
            {
                Player.ShowPopup("Online Staff", "There are no Admins online. If you desperately need one, try our TS3 channel.");
            }
        }

        [ChatCommand("kick")]
        private void Kick(Player Player, string cmd, string[] args)
        {
            if (Administrators.Contains(Player.Id))
            {
                string PlayerToKick = args[0].ToLower();
                string Reason = MergeParams(args, 1).ToLower();

                if (!Administrators.Contains(Server.GetPlayerByName(PlayerToKick).Id))
                {
                    Player ToKick = Server.GetPlayerByName(PlayerToKick);

                    if (ToKick != null)
                    {
                        Server.Kick(ToKick, Reason, false);
                        PrintToChat("Server: {0} has kicked {1} for {2}", Player.DisplayName, ToKick.DisplayName, (!string.IsNullOrEmpty(Reason) ? Reason : "No reason specified."));
                    }
                }
            }
        }

        private void SendHelpText(Player player)
        {
            PrintToChat(player, "If you need help with the mods, use /modhelp");
        }

        [ChatCommand("modhelp")]
        private void ModHelp(Player Player, string Command, string[] Args)
        {
            if (Args.Length >= 1)
            {
                string Argument = MergeParams(Args, 0);

                switch (Argument.ToLower())
                {
                    case "balance":
                        {
                            PrintToChat(Player, "Help for /balance:");
                            PrintToChat(Player, "Views your current balance. Usage: /balance");

                            break;
                        }

                    case "buy":
                        {
                            PrintToChat(Player, "Help for /buy:");
                            PrintToChat(Player, "Buys an item from the store. Usage: /buy <amount> <item>");

                            break;
                        }

                    case "bounties":
                        {
                            PrintToChat(Player, "Help for /bounties:");
                            PrintToChat(Player, "Views the current bounties. Usage: /bounties");

                            break;
                        }

                    case "setbounty":
                        {
                            PrintToChat(Player, "Help for /setbounty:");
                            PrintToChat(Player, "Sets a bounty for in-game currency. Usage: /setbounty <amount 1-1000> <username>");

                            break;
                        }

                    case "pay":
                        {
                            PrintToChat(Player, "Help for /pay:");
                            PrintToChat(Player, "Pays other players in-game currency. Usage: /pay <amount> <username>");

                            break;
                        }

                    case "listitems":
                        {
                            PrintToChat(Player, "Help for /listitems:");
                            PrintToChat(Player, "Shows you the items for sale in the shop. Usage: /listitems selling/buying");

                            break;
                        }

                    case "pm":
                        {
                            PrintToChat(Player, "Help for /pm:");
                            PrintToChat(Player, "Private messages another player. Usage: /pm \"<username>\" <message>");

                            break;
                        }

                    case "guild":
                        {
                            PrintToChat(Player, "Help for /guild:");
                            PrintToChat(Player, "Checks the guild of another player. Usage: /guild \"<username>\"");

                            break;
                        }

                    default:
                        {
                            PrintToChat(Player, "Use /modhelp <command> to view details. Available commands: ");
                            PrintToChat(Player, "/listitems /pay /setbounty /bounties /buy /balance");

                            break;
                        }
                }
            }
            else
            {
                PrintToChat(Player, "Use /modhelp <command> to view details. Available commands: ");
                PrintToChat(Player, "listitems pay setbounty bounties buy balance pm guild");
            }
        }

        [ChatCommand("loc")]
        private void LocationCommand(Player player, string cmd, string[] args)
        {
            PrintToChat(player, string.Format("Current Location: x:{0} y:{1} z:{2}", player.Entity.Position.x.ToString(), player.Entity.Position.y.ToString(), player.Entity.Position.z.ToString()));
        }

        [ChatCommand("players")]
        private void Players(Player Player, string Command, string[] Args)
        {
            List<Player> PlayersOnline = Server.OtherPlayers;
            int PlayerCount = PlayersOnline.Count;
            int i = 1;

            string Output = "Online Players:\r\n\r\n";

            foreach (Player Players in PlayersOnline)
            {
                if (i < PlayerCount)
                {
                    Output += "- " + Players.DisplayName + "\r\n";
                }
                else
                {
                    Output += "- " + Players.DisplayName;
                }

                i++;
            }

            Player.ShowPopup("Online Players", Output);
        }

        [ChatCommand("buy")]
        private void BuyItem(Player Player, string Command, string[] Args)
        {
            if (Args.Length >= 2)
            {
                int ItemAmount = Int32.TryParse(Args[0], out ItemAmount) ? ItemAmount : 0;
                int ItemId = 1;
                int AmountLeft = 0;
                int ItemPrice = 0;

                InvItemBlueprint ProposedBlueprint = InvDefinitions.Instance.Blueprints.GetBlueprintForName(MergeParams(Args, 1), true, true);
                InvItemBlueprint Blueprint = null;

                foreach (InvItemBlueprint Blueprints in Marketplace.Items.Values)
                {
                    if (Blueprints == ProposedBlueprint)
                    {
                        Blueprint = Blueprints;
                        AmountLeft = Marketplace.ItemAmounts[ItemId];
                        ItemPrice = Marketplace.ItemBuyPrices[ItemId];

                        break;
                    }

                    ItemId++;
                }

                if (ItemAmount > AmountLeft)
                {
                    PrintToChat(Player, "Marketplace: There is only " + AmountLeft.ToString() + " of that item left. You tried to buy: " + ItemAmount.ToString());
                    return;
                }

                if (Blueprint != null && ItemAmount > 0 && ItemAmount < 1001)
                {
                    AmpCache Me = GetCache(Player);
                    int MyCurrency = Me.CurrencyAmount;

                    int ItemPricePerSingle = ItemPrice;
                    int TotalPriceCost = ItemPricePerSingle;

                    if (ItemAmount > 1)
                        TotalPriceCost = ItemAmount * ItemPricePerSingle;

                    Container Inventory = Player.Entity.GetContainerOfType(CollectionTypes.Inventory);
                    ContainerManagement ContManager = Blueprint.TryGet<ContainerManagement>();

                    if (ItemAmount > ContManager.StackLimit)
                    {
                        ItemAmount = ContManager.StackLimit;
                        TotalPriceCost = ItemAmount * ItemPricePerSingle;
                    }

                    if (MyCurrency >= TotalPriceCost)
                    {
                        if (Inventory.Contents.FreeSlotCount > 0)
                        {
                            if (!ContManager.Stackable)
                            {
                                ItemAmount = 1;
                            }

                            Inventory.Contents.AddItem(new InvGameItemStack(Blueprint, ItemAmount, null));

                            PrintToChat(Player, "Marketplace: You have purchased {0} {1} for {2} {3}", ItemAmount, Blueprint.Name, TotalPriceCost, CurrencyName);

                            Me.CurrencyAmount -= TotalPriceCost;

                            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
                            {
                                Marketplace.UpdateItemCount(ItemId, Marketplace.ItemAmounts[ItemId] - ItemAmount, Connection);
                            }
                        }
                        else
                        {
                            PrintToChat(Player, "Marketplace: You need to make some room in your inventory to buy this.");
                        }
                    }
                    else
                    {
                        PrintToChat(Player, "Marketplace: You can't afford to purchase this.");
                    }
                }
                else
                {
                    PrintToChat(Player, "Marketplace: You can check which items are for sale by typing /listitems");
                }
            }
            else
            {
                PrintToChat(Player, "Usage: /buy amount itemname");
            }
        }

        [ChatCommand("a")]
        void AdminSpeak(Player Player, string Command, string[] Args)
        {
            AmpCache Me = GetCache(Player);

            if (Me != null)
            {
                if (Administrators.Contains(Player.Id))
                {
                    SendMessageToAdmins(MergeParams(Args, 0), true, Player);
                }
            }
        }

        [ChatCommand("sell")]
        private void Sell(Player Player, string Command, string[] Args)
        {
            if (Args.Length >= 2)
            {
                int Amount = Convert.ToInt32(Args[0]);
                string Name = MergeParams(Args, 1);
                int AmountOfItem = 0;
                int MarketplaceId = 0;
                int AmountToRefund = 0;

                AmpCache Me = GetCache(Player);

                Container Inventory = Player.Entity.GetContainerOfType(CollectionTypes.Inventory);
                List<InvGameItemStack> InventoryItems = Inventory.Contents.GetItems();
                List<InvGameItemStack> ToRemove = new List<InvGameItemStack>();

                foreach (InvGameItemStack Item in InventoryItems)
                {
                    if (Item.Name.ToLower() == Name.ToLower())
                    {
                        AmountOfItem += Item.StackAmount;
                        ToRemove.Add(Item);
                        break;
                    }

                    continue;
                }

                if (AmountOfItem > 0 && AmountOfItem >= Amount)
                {
                    foreach (int MarketId in Marketplace.Items.Keys)
                    {
                        if (Marketplace.Items[MarketId].Name.ToLower() == Name.ToLower())
                        {
                            MarketplaceId = MarketId;
                            break;
                        }

                        continue;
                    }

                    AmountToRefund = AmountOfItem - Amount;

                    if (MarketplaceId > 0 && Amount > 0 && Amount < 1001)
                    {
                        int Profit = Marketplace.ItemSellPrices[MarketplaceId] * Amount;

                        Me.CurrencyAmount += Profit;

                        using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
                        {
                            Marketplace.UpdateItemCount(MarketplaceId, Marketplace.ItemAmounts[MarketplaceId] + Amount, Connection);
                        }

                        foreach (InvGameItemStack ItemStack in ToRemove)
                        {
                            Inventory.Contents.RemoveItem(ItemStack);
                        }

                        if (AmountToRefund > 0)
                        {
                            InvGameItemStack ItemGameStack = new InvGameItemStack(Marketplace.Items[MarketplaceId], AmountToRefund, null);
                            Inventory.Contents.AddItem(ItemGameStack);
                        }

                        PrintToChat(Player, "Marketplace: You have sold " + Amount.ToString() + " " + Name + " for " + Profit.ToString() + " " + CurrencyName);
                    }
                    else
                    {
                        PrintToChat(Player, "Marketplace: This item cannot be sold on the Marketplace, or you are trying to sell more than 1000 at once.");
                    }
                }
                else
                {
                    PrintToChat(Player, "Marketplace: You do not have the amount of " + Name + " you are trying to sell. Please ensure it is all in 1 stack if possible.");
                }
            }
            else
            {
                PrintToChat(Player, "Usage: /sell <amount> <item>");
            }
        }

        [ChatCommand("listitems")]
        private void ListItems(Player Player, string Command, string[] Args)
        {
            if (Args.Length < 1)
            {
                Player.ShowPopup("Hello!", "We have updated our Shop system.\r\n\r\nIf you are buying items, type \"/listitems buying\" to view the prices.\r\n\r\nIf you want to sell items, type \"/listitems selling\" to view the prices.");
                return;
            }

            string Type = MergeParams(Args, 0).ToLower();

            switch (Type)
            {
                case "buying":
                case "buy":
                    {
                        string Output = "[B2B200]All items are sold and purchased in quantities of 1.[-]\r\n\r\n";

                        Output += "[0066FF]Purchase Prices[-] [CC0000](buy from us)[-]\r\n------------------\r\n";

                        foreach (int Id in Marketplace.Items.Keys)
                        {
                            Output += Marketplace.Items[Id].Name + " - " + Marketplace.ItemBuyPrices[Id] + " " + CurrencyName + " - Quantity left: " + Marketplace.ItemAmounts[Id].ToString() + "\r\n";
                        }

                        Output += "\r\n\r\n[B2B200]You can use /buy <amount> <item name> to purchase from this list.[-]";

                        Player.ShowPopup("Items we are Selling", Output);
                        break;
                    }

                case "selling":
                case "sell":
                    {
                        string Output = "[B2B200]All items are sold and purchased in quantities of 1.[-]\r\n\r\n";

                        Output += "\r\n[0066FF]Selling Prices[-] [CC0000](sell to us)[-]\r\n------------------\r\n";

                        foreach (int SellId in Marketplace.Items.Keys)
                        {
                            Output += Marketplace.Items[SellId].Name + " - " + Marketplace.ItemSellPrices[SellId] + " " + CurrencyName + "\r\n";
                        }

                        Output += "\r\n\r\n[B2B200]You can use /sell <amount> <item name> to sell items on this list.[-]";

                        Player.ShowPopup("Items we are Buying ", Output);
                        break;
                    }
            }

        }

        [ChatCommand("setbounty")]
        private void SetBounty(Player Player, string Command, string[] Args)
        {
            if (Args.Length >= 2)
            {
                int Amount = Int32.TryParse(Args[0], out Amount) ? Amount : 0;
                string Playername = MergeParams(Args, 1);
                int MyCurrency = 0;

                Player OtherPlayer = Server.GetPlayerByName(Playername);

                if (OtherPlayer != null && Server.PlayerIsOnline(Playername))
                {
                    if (!Bounties.ContainsKey(OtherPlayer))
                    {
                        AmpCache Me = GetCache(Player);
                        MyCurrency = Me.CurrencyAmount;

                        if (Amount > 0 && Amount < 1001)
                        {
                            if (MyCurrency >= Amount)
                            {
                                Bounties.Add(OtherPlayer, Amount);
                                PrintToChat("{0} has placed a bounty of {1} {2} on {3}", Player.DisplayName, Amount, CurrencyName, OtherPlayer.DisplayName);
                                Me.CurrencyAmount -= Amount;
                            }
                            else
                            {
                                PrintToChat(Player, "You cannot afford to place this bounty.");
                            }
                        }
                        else
                        {
                            PrintToChat(Player, "A bounty must be set to a value higher than 1 and lower than 1001.");
                        }
                    }
                    else
                    {
                        PrintToChat(Player, "This player already has a bounty set on them.");
                    }
                }
                else
                {
                    PrintToChat(Player, "That player either does not exist or is not online.");
                }
            }
            else
            {
                PrintToChat(Player, "Usage: /setbounty amount playername");
            }
        }

        [ChatCommand("bounties")]
        private void ViewBounties(Player Player, string Command, string[] Args)
        {
            PrintToChat(Player, "Current Bounties");

            if (Bounties.Count > 0)
            {
                foreach (Player Playa in Bounties.Keys)
                {
                    PrintToChat(Player, Playa.DisplayName + " for " + Bounties[Playa].ToString() + " " + CurrencyName);
                }
            }
            else
            {
                PrintToChat(Player, "There are no bounties at the moment.");
            }
        }

        [ChatCommand("balance")]
        private void CheckCurrencyBalance(Player Player, string Command, string[] Arguments)
        {
            PrintToChat(Player, "Economy: You currently have {0} {1}", GetCache(Player).CurrencyAmount, CurrencyName);
        }

        [ChatCommand("guild")]
        private void CheckGuild(Player Player, string Command, string[] Arguments)
        {
            if (Arguments.Length < 1)
            {
                PrintToChat(Player, "Usage: /guild <username>");
            }
            else
            {
                Player ThePlayer = Server.GetPlayerByName(MergeParams(Arguments, 0));

                if (ThePlayer != null)
                {
                    string Guild = PlayerExtensions.GetGuild(ThePlayer).DisplayName;
                    PrintToChat(Player, "{0}'s guild is: {1}", ThePlayer.DisplayName, Guild);
                }
            }
        }

        [ChatCommand("pm")]
        private void PrivateMessage(Player Player, string Command, string[] Arguments)
        {
            string Playername = Arguments[0];
            string Message = MergeParams(Arguments, 1);

            Player Entity = Server.GetPlayerByName(Playername);

            if (Entity != null && Server.PlayerIsOnline(Playername))
            {
                PrintToChat(Entity, "[FFFF00]PM from {0}: [-]{1}", Player.DisplayName, Message);
                PrintToChat(Player, "[FFFF00]PM sent to {0}: [-]{1}", Entity.DisplayName, Message);
            }
        }

        [ChatCommand("makesay")]
        private void MakeSay(Player Player, string Command, string[] Arguments)
        {
            Player PlayerToBe = Server.GetPlayerByName(Arguments[0]);
            string Message = MergeParams(Arguments, 1);

            if (Administrators.Contains(Player.Id))
            {
                PrintToChat("{0} : {1}", PlayerToBe.DisplayName, Message);
            }
        }

        [ChatCommand("give")]
        private void GiveDecoy(Player Player, string Command, string[] Arguments)
        {
            if (Administrators.Contains(Player.Id))
            {
                SendMessageToAdmins(Player.DisplayName + " just tried to spawn " + Arguments[0]);
            }
        }

        [ChatCommand("warn")]
        private void WarnPlayer(Player Player, string Command, string[] Arguments)
        {
            if (Administrators.Contains(Player.Id))
            {
                Player ToWarnEntity = Server.GetPlayerByName(MergeParams(Arguments, 0));
                AmpCache ToWarn = GetCache(ToWarnEntity);

                if (ToWarn.Warnings + 1 >= 3)
                {
                    ToWarn.Warnings = 0;
                    UpdateDatabase(ToWarnEntity);

                    Server.Ban(ToWarnEntity, 1, "3 Warnings - Temporarily Banned");
                }
                else
                {
                    ToWarn.Warnings += 1;

                    PrintToChat("[" + ChatColours.Red + "]" + ToWarnEntity.DisplayName + " has been warned by " + Player.DisplayName + "[-]");
                    PrintToChat(ToWarnEntity, "[" + ChatColours.Yellow + "]You have been warned by " + Player.DisplayName + ", 3 warnings = 1 day temporary ban.[-]");
                }
            }
        }

        [ChatCommand("pay")]
        private void PayPlayerCurrency(Player Player, string Command, string[] Arguments)
        {
            int AmountToPay = Int32.TryParse(Arguments[0], out AmountToPay) ? AmountToPay : 0;
            string OtherPlayer = MergeParams(Arguments, 1);

            Player ToPay = Server.GetPlayerByName(OtherPlayer);
            AmpCache CachedPlayer = GetCache(ToPay);
            AmpCache Me = GetCache(Player);

            if (ToPay != null && Server.PlayerIsOnline(ToPay.DisplayName) && Player.DisplayName != ToPay.DisplayName)
            {
                if (Me.CurrencyAmount >= AmountToPay && AmountToPay > 0)
                {
                    Me.CurrencyAmount -= AmountToPay;
                    CachedPlayer.CurrencyAmount += AmountToPay;

                    PrintToChat(ToPay, "[" + ChatColours.Yellow + "]You have been paid " + AmountToPay.ToString() + " " + CurrencyName + " by " + Player.DisplayName + "[-]");
                    PrintToChat(Player, "[" + ChatColours.Yellow + "]You have paid " + ToPay.DisplayName + " " + AmountToPay.ToString() + " " + CurrencyName + "[-]");
                }
                else
                {
                    PrintToChat(Player, "You cannot afford this payment, or the payment amount was lower than 1.");
                }
            }
            else
            {
                PrintToChat(Player, "That player does not exist, is offline, or you are trying to pay yourself. Please try again.");
            }
        }

        [ChatCommand("modreload")]
        private void ReloadCommand(Player Player, string Command, string[] Arguments)
        {
            if (CheckAdminStatus(Player))
            {
                string ToReload = MergeParams(Arguments, 0);

                switch (ToReload.ToLower())
                {
                    case "marketplace":
                        {
                            using (MySqlConnection Connection = new MySqlConnection(ConnectionString))
                            {
                                Marketplace.RefreshMarketplace(Connection);
                                PrintToChat(Player, "[" + ChatColours.Green + "]You have reloaded the marketplace successfully.[-]");
                            }

                            break;
                        }

                    case "config":
                        {
                            LoadConfigFromDatabase();
                            PrintToChat(Player, "[" + ChatColours.Green + "]You have reloaded the config successfully.[-]");
                            break;
                        }

                    case "rules":
                        {
                            LoadGuidelines();
                            PrintToChat(Player, "[" + ChatColours.Green + "]You have reloaded the rules successfully.[-]");
                            break;
                        }

                    case "announcements":
                        {
                            LoadAnnouncements();
                            PrintToChat(Player, "[" + ChatColours.Green + "]You have reloaded the announcements successfully.[-]");
                            break;
                        }

                    default:
                        {
                            PrintToChat(Player, "[" + ChatColours.Red + "]Valid arguments: marketplace,config,rules,announcements[-]");
                            break;
                        }
                }
            }
        }

        private string MergeParams(string[] Params, int Start)
        {
            StringBuilder MergedParams = new StringBuilder();

            for (int i = 0; i < Params.Length; i++)
            {
                if (i < Start)
                {
                    continue;
                }

                if (i > Start)
                {
                    MergedParams.Append(" ");
                }

                MergedParams.Append(Params[i]);
            }

            return MergedParams.ToString();
        }
    }
}