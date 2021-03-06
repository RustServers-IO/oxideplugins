using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("TwitchAuth", "Wulf/lukespragg", "0.1.3", ResourceId = 1838)]
    [Description("Only allow your followers on Twitch to join your server")]
    public class TwitchAuth : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Twitch account name")]
            public string TwitchAccount;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    TwitchAccount = ""
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.TwitchAccount == null) LoadDefaultConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandTwitchAuth"] = "twitchauth",
                ["CommandUsage"] = "Usage: {0} <Twitch account name>",
                ["IsExcluded"] = "{0} is excluded from Twitch auth check",
                ["IsFollowing"] = "{0} is a Twitch follower, allowing connection",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NotFollowing"] = "{0} tried to join, but is not a Twitch follower",
                ["NotLinked"] = "Please follow @ twitch.tv/{0} and link to Steam to join",
                ["TwitchAccountSet"] = "Twitch account set to: {0}"
            }, this);
        }

        #endregion

        #region Initialization

        private const string permAdmin = "twitchauth.admin";
        private const string permExclude = "twitchauth.exclude";

        public class TwitchUser
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permExclude, this);

            AddCommandAliases("CommandTwitchAuth", "TwithAuthCommand");
        }

        #endregion

        #region Commands

        private void TwithAuthCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            config.TwitchAccount = args[0];
            Message(player, "TwitchAccountSet", args[0]);
            SaveConfig();
        }

        #endregion

        #region Twitch Handling

        private void OnUserConnected(IPlayer player)
        {
            if (player.HasPermission(permExclude))
            {
                Puts(Lang("IsExcluded", null, player.Name));
                return;
            }

            UserHasSteam(player);
        }

        private void UserHasSteam(IPlayer player)
        {
            webrequest.EnqueueGet($"https://api.twitch.tv/api/steam/{player.Id}", (code, response) =>
                UserHasSteamConnectedCallBack(code, response, player), this);
        }

        private void UserHasSteamConnectedCallBack(int code, string response, IPlayer player)
        {
            if (code != 200 && !string.IsNullOrEmpty(response))
            {
                player.Kick(Lang("NotLinked", player.Id, config.TwitchAccount));
                Puts(Lang("NotFollowing", null, player.Name));
                return;
            }

            var twitchUser = JsonConvert.DeserializeObject<TwitchUser>(response);
            if (!string.IsNullOrEmpty(twitchUser?.Name)) UserIsFollowing(twitchUser.Name, player);
        }

        private void UserIsFollowing(string name, IPlayer player)
        {
            webrequest.EnqueueGet($"https://api.twitch.tv/kraken/users/{name}/follows/channels/{config.TwitchAccount}", (code, response) =>
                UserIsFollowingCallBack(code, response, player), this);
        }

        private void UserIsFollowingCallBack(int code, string response, IPlayer player)
        {
            if (code != 200 && !string.IsNullOrEmpty(response))
            {
                player.Kick(Lang("Rejection", player.Id, config.TwitchAccount));
                Puts(Lang("NotFollowing", null, player.Name));
            }
            else
                Puts(Lang("IsFollowing", null, player.Name));
        }

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.Equals(key)))
                    if (!string.IsNullOrEmpty(message.Value)) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion
    }
}
