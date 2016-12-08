using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ConnectMessages", "Spicy", "1.1.5")]
    [Description("Provides connect and disconnect messages.")]

    class ConnectMessages : CovalencePlugin
    {
        #region Country API Class

        private class Response
        {
            [JsonProperty("country")]
            public string Country;
        }

        #endregion

        #region Config

        private bool showConnectMessage;
        private bool showConnectCountry;
        private bool showDisconnectMessage;
        private bool showDisconnectReason;
        private bool showAdminMessages;

        private bool GetConfigValue(string key) => Config.Get<bool>("Settings", key);

        protected override void LoadDefaultConfig()
        {
            Config["Settings"] = new Dictionary<string, bool>
            {
                ["ShowConnectMessage"] = true,
                ["ShowConnectCountry"] = false,
                ["ShowDisconnectMessage"] = true,
                ["ShowDisconnectReason"] = false,
                ["ShowAdminMessages"] = true
            };
        }

        private void InitialiseConfig()
        {
            showConnectMessage = GetConfigValue("ShowConnectMessage");
            showConnectCountry = GetConfigValue("ShowConnectCountry");
            showDisconnectMessage = GetConfigValue("ShowDisconnectMessage");
            showDisconnectReason = GetConfigValue("ShowDisconnectReason");
            showAdminMessages = GetConfigValue("ShowAdminMessages");
        }

        #endregion

        #region Lang

        private string GetLangValue(string key, string userId) => lang.GetMessage(key, this, userId);

        private void InitialiseLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConnectMessage"] = "{0} has connected.",
                ["ConnectMessageCountry"] = "{0} has connected from {1}.",
                ["DisconnectMessage"] = "{0} has disconnected.",
                ["DisconnectMessageReason"] = "{0} has disconnected. ({1})"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConnectMessage"] = "{0} s'est connecté(e).",
                ["ConnectMessageCountry"] = "{0} s'est connecté(e) de {1}.",
                ["DisconnectMessage"] = "{0} s'est disconnecté(e).",
                ["DisconnectMessageReason"] = "{0} s'est disconnecté(e). ({1})"
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConnectMessage"] = "{0} ha conectado.",
                ["ConnectMessageCountry"] = "{0} se ha conectado de {1}.",
                ["DisconnectMessage"] = "{0} se ha desconectado.",
                ["DisconnectMessageReason"] = "{0} se ha desconectado. ({1})"
            }, this, "es");
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
#if HURTWORLD
            GameManager.Instance.ServerConfig.ChatConnectionMessagesEnabled = false;
#endif

            InitialiseConfig();
            InitialiseLang();
        }

        private void OnUserConnected(IPlayer player)
        {
            if (!showConnectMessage || (player.IsAdmin && !showAdminMessages))
                return;

            if (!showConnectCountry)
            {
                foreach (IPlayer _player in players.Connected)
                    _player.Message(string.Format(GetLangValue("ConnectMessage", _player.Id), player.Name.Sanitize()));

                return;
            }

            string apiUrl = "http://ip-api.com/json/";

            webrequest.EnqueueGet(apiUrl + player.Address, (code, response) =>
            {
                if (code != 200)
                {
                    Puts($"WebRequest to {apiUrl} failed.");
                    return;
                }

                string country = JsonConvert.DeserializeObject<Response>(response).Country;

                foreach (IPlayer _player in players.Connected)
                    _player.Message(string.Format(GetLangValue("ConnectMessageCountry", _player.Id), player.Name.Sanitize(), country));
            }, this);
        }

        private void OnUserDisconnected(IPlayer player, string reason)
        {
            if (!showDisconnectMessage || (player.IsAdmin && !showAdminMessages))
                return;

            foreach (IPlayer _player in players.Connected)
            {
                if (!showDisconnectReason)
                    _player.Message(string.Format(GetLangValue("DisconnectMessage", _player.Id), player.Name.Sanitize()));
                else
                    _player.Message(string.Format(GetLangValue("DisconnectMessageReason", _player.Id), player.Name.Sanitize(), reason));
            }
        }

        #endregion
    }
}
