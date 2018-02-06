using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Analytics", "Wulf/lukespragg", "1.1.0", ResourceId = 679)]
    [Description("Real-time collection and reporting of server data to Google Analytics")]
    public class Analytics : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Google tracking ID (ex. UA-XXXXXXXX-Y)")]
            public string TrackingId;

            [JsonProperty(PropertyName = "Track connections (true/false)")]
            public bool TrackConnections;

            [JsonProperty(PropertyName = "Track disconnections (true/false)")]
            public bool TrackDisconnections;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    TrackingId = "",
                    TrackConnections = true,
                    TrackDisconnections = true
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.TrackingId == null) LoadDefaultConfig();
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

        #region Google Analytics

        private static Dictionary<string, string> headers = new Dictionary<string, string>
        {
            ["User-Agent"] = $"Oxide/{OxideMod.Version} ({Environment.OSVersion}; {Environment.OSVersion.Platform})"
        };

        private void Collect(IPlayer player, string session)
        {
            if (string.IsNullOrEmpty(config.TrackingId))
            {
                LogWarning("Google tracking ID is not set, analytics will not be collected");
                return;
            }

            var url = "https://www.google-analytics.com/collect";
            var data = $"v=1&tid={config.TrackingId}&sc={session}&t=screenview&cd={server.Name}&an={covalence.Game}" +
                $"&av={covalence.Game}+v{server.Version}+({server.Protocol})&cid={player.Id}&ul={player.Language}";

            webrequest.EnqueuePost(url, Uri.EscapeUriString(data), (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    LogWarning($"URL: {url}");
                    LogWarning($"Data: {data}");
                    LogWarning($"HTTP code: {code}");
                    LogWarning($"Response: {response}");
                }
            }, this, headers);
        }

        #endregion

        #region Event Collection

        private void CollectAll(string session)
        {
            foreach (var player in players.Connected) Collect(player, "start");
        }

        private void OnServerInitialized() => CollectAll("start");

        private void OnServerSave() => CollectAll("start");

        private void OnUserConnected(IPlayer player) => Collect(player, "start");

        private void OnUserDisconnected(IPlayer player) => Collect(player, "end");

        #endregion
    }
}
