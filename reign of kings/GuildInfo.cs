using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeHatch.Engine.Networking;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Common;
using Oxide.Core;
using CodeHatch.Engine.Modules.SocialSystem;

namespace Oxide.Plugins
{
    [Info("GuildInfo", "Scorpyon & D-Kay", "1.2.0", ResourceId = 1125)]
    public class GuildInfo : ReignOfKingsPlugin
    {
        #region Variables

        public bool useAdminOnly { get; private set; }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadDefaultMessages();
            LoadConfigData();
            permission.RegisterPermission("guildinfo.guild", this);
            permission.RegisterPermission("guildinfo.player", this);
            permission.RegisterPermission("guildinfo.toggle", this);
        }

        protected override void LoadDefaultConfig()
        {
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            useAdminOnly = GetConfig("useAdminOnly", false);
        }

        private void SaveConfigData()
        {
            Config["useAdminOnly"] = useAdminOnly;
            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "InvalidArgsGuild", "[FF0000]Guild Master[FFFFFF] : To find out about a guild, type [00FF00]/guildinfo [FF00FF]<guildname>." },
                { "InvalidArgsPlayer", "[FF0000]Guild Master[FFFFFF] : To find out about a player's guild, type [00FF00]/playerinfo [FF00FF]<playername>." },
                { "GuildlistTitle", "[FF0000]Guild Master[FFFFFF] : [00FF00]{0}[FFFFFF] has {1} members:" },
                { "GuildlistIsOffline", "[008888] (Offline)" },
                { "GuildlistIsOnline", "[FF0000] (Online)" },
                { "GuildlistPlayerStatus", "[00FF00]{0}{1}" },
                { "NoPlayer", "[FF0000]Guild Master[FFFFFF] : Unable to find the guild of that player." },
                { "NoGuild", "[FF0000]Guild Master[FFFFFF] : Unable to find that guild." },
                { "NoPermission", "[FF0000]Guild Master[FFFFFF] : You do not have permission to use this command." },
                { "HelpTextTitle", "[0000FF]Guildinfo Commands[FFFFFF]" },
                { "HelpTextGuildinfo", "[00FF00]/guildinfo (guildname)[FFFFFF] - shows the guildname, players and their online status of a guild." },
                { "HelpTextPlayerinfo", "[00FF00]/playerinfo (playername)[FFFFFF] - shows the guildname, players and their online status of a player." },
                { "ToggleAdminOnly", "[FF0000]Guild Master[FFFFFF] : Admin only usage of commands was turned {0}." }
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("guildinfo")]
        private void ShowGuildInfo(Player player, string cmd, string[] args)
        {
            if (useAdminOnly && !player.HasPermission("guildinfo.guild")) { GetMessage("NoPermission", player.Id.ToString()); return; }

            if (args.Length < 1)
            {
                PrintToChat(player, GetMessage("InvalidArgs", player.Id.ToString()));
                return;
            }
            string name = ConvertArrayToString(args);
            ShowListByGuild(player, name);
        }

        [ChatCommand("playerinfo")]
        private void ShowPlayerInfo(Player player, string cmd, string[] args)
        {
            if (useAdminOnly && !player.HasPermission("guildinfo.player")) { GetMessage("NoPermission", player.Id.ToString()); return; }
            if (args.Length < 1)
            {
                PrintToChat(player, GetMessage("InvalidArgs", player.Id.ToString()));
                return;
            }
            string name = ConvertArrayToString(args);
            ShowListByPlayer(player, name);
        }
        
        [ChatCommand("giadminonly")]
        private void ToggleAdminOnly(Player player, string cmd, string[] args)
        {
            if (useAdminOnly && !player.HasPermission("guildinfo.toggle")) { GetMessage("NoPermission", player.Id.ToString()); return; }
            if (useAdminOnly) { useAdminOnly = false; PrintToChat(player, string.Format(GetMessage("ToggleAdminOnly", player.Id.ToString()), "off")); }
            else { useAdminOnly = true; PrintToChat(player, string.Format(GetMessage("ToggleAdminOnly", player.Id.ToString()), "on")); }
        }

        #endregion

        #region Functions

        private void ShowListByGuild(Player player, string guildName)
        {
            Guild guild = GetGuildByGuild(guildName);
            if(guild == null) { PrintToChat(player, GetMessage("NoGuild", player.Id.ToString())); return; }
            ListGuildInfo(guild, player);
        }

        private void ShowListByPlayer(Player player, string playerName)
        {
            Guild guild = GetGuildByPlayer(playerName);
            if (guild == null) { PrintToChat(player, GetMessage("NoPlayer", player.Id.ToString())); return; }
            ListGuildInfo(guild, player);
        }

        private void ListGuildInfo(Guild guild, Player player)
        {
            ReadOnlyCollection<Member> members = guild.Members().GetAllMembers();
            PrintToChat(player, string.Format(GetMessage("GuildlistTitle", player.Id.ToString()), guild.DisplayName, members.Count));
            foreach (Member member in members)
            {
                string onlineStatus = GetMessage("GuildlistIsOffline", player.Id.ToString());
                if (member.OnlineStatus) onlineStatus = GetMessage("GuildlistIsOnline", player.Id.ToString());
                PrintToChat(player, string.Format(GetMessage("GuildlistPlayerStatus", player.Id.ToString()), member.Name, onlineStatus));
            }
        }

        private Guild GetGuildByPlayer(Player player)
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();
            Guild guild = guildScheme.TryGetGuildByMember(player.Id);
            return guild;
        }

        private Guild GetGuildByPlayer(ulong player)
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();
            Guild guild = guildScheme.TryGetGuildByMember(player);
            return guild;
        }

        private Guild GetGuildByPlayer(string player)
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();
            
            List<Guild> guilds = GetAllGuilds();
            foreach (Guild guild in guilds)
            {
                ReadOnlyCollection<Member> members = guild.Members().GetAllMembers();
                foreach (Member member in members)
                {
                    if (member.Name.ToLower().Contains(player.ToLower()))
                    {
                        return guild;
                    }
                }
            }
            return null;
        }

        private Guild GetGuildByGuild(string guildName)
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();

            List<Guild> guilds = GetAllGuilds();
            foreach (Guild guild in guilds)
            {
                if (guild.DisplayName.ToLower().Contains(guildName.ToLower())) return guild;
            }
            return null;
        }

        private List<Guild> GetAllGuilds()
        {
            GuildScheme guildScheme = SocialAPI.Get<GuildScheme>();

            List<RootSocialGroup> groups = guildScheme.Storage.TryGetAllGroups();
            List<Guild> guilds = new List<Guild>();
            foreach (RootSocialGroup group in groups)
            {
                Guild guild = guildScheme.TryGetGuild(group.BaseID);
                if (guild != null) guilds.Add(guild);
            }
            return guilds;
        }

        #endregion

        #region Hooks

        private void SendHelpText(Player player)
        {
            PrintToChat(player, GetMessage("HelpTextTitle", player.Id.ToString()));
            PrintToChat(player, GetMessage("HelpTextGuildinfo", player.Id.ToString()));
            PrintToChat(player, GetMessage("HelpTextPlayerinfo", player.Id.ToString()));
        }

        #endregion

        #region Utility

        private string ConvertArrayToString(string[] args)
        {
            string name = args[0];
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    name = name + " " + args[i];
                }
            }
            return name;
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}
