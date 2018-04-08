using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using Version = Oxide.Core.VersionNumber;

namespace Oxide.Plugins
{
    [Info("Update Checker", "LaserHydra", "2.2.1")]
    [Description("Checks for and notifies of any outdated plugins")]
    public sealed class UpdateChecker : CovalencePlugin
    {
        #region Fields

        private const string PluginInformationUrl = "http://oxide.laserhydra.com/plugins/{resourceId}/";

        [PluginReference]
        private Plugin EmailAPI, PushAPI;

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            LoadConfig();

            timer.Repeat(GetConfig(60f, "Settings", "Auto Check Interval (in Minutes)") * 60, 0, () => CheckForUpdates(null));
            CheckForUpdates(null);
        }

        #endregion

        #region Loading

        private new void LoadConfig()
        {
            SetConfig("Settings", "Auto Check Interval (in Minutes)", 60f);
            SetConfig("Settings", "Use PushAPI", false);
            SetConfig("Settings", "Use EmailAPI", false);

            SaveConfig();
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Checking", "Checking for updates... This may take a few seconds. Please be patient."},
                {"Outdated Plugin List", "Following plugins are outdated:\n{plugins}"},
                {"Outdated Plugin Info", "# {title} | Installed: {installed} - Latest: {latest} | {url}"},
                {"Missing ResourceId", "Following plugins are missing their resourceId, and therefor cannot be checked for updates: {plugins}"},
                {"Resource Unavailable", "Following plugins are not accessible online at the moment, and therefore cannot be checked for updates: {plugins}"},
                {"Resource Details Unavailable", "Following plugins have an improper version number else may not have a release version available, and therefore cannot be checked for updates: {plugins}"},
            }, this);
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        #endregion

        #region Commands

        [Command("updates"), Permission("updatechecker.use")]
        private void CmdUpdates(IPlayer player, string cmd, string[] args)
        {
            SendMessage(player, GetMsg("Checking", player.Id));
            CheckForUpdates(player);
        }

        #endregion

        #region Notifications

        private void Notify(IPlayer player, string message)
        {
            if (player == null && GetConfig(false, "Settings", "Use PushAPI"))
                PushAPI?.Call("PushMessage", "Plugin Update Notification", message);

            if (player == null && GetConfig(false, "Settings", "Use EmailAPI"))
                EmailAPI?.Call("EmailMessage", "Plugin Update Notification", message);

            SendMessage(player, message);
        }

        private void SendMessage(IPlayer player, string message)
        {
            if (player != null)
                player.Reply(message);
            else
                PrintWarning(message);
        }

        #endregion

        #region Update Checks

        public void CheckForUpdates(IPlayer requestor)
        {
            var outdatedPlugins = new Dictionary<Plugin, ApiResponse.Data>();
            var failures = new Dictionary<string, List<Plugin>>
            {
                ["Missing ResourceId"] = new List<Plugin>(),
                ["Resource Unavailable"] = new List<Plugin>(),
                ["Resource Details Unavailable"] = new List<Plugin>()
            };

            var totalPlugins = plugins.GetAll().Length;
            var currentPlugin = 1;

            foreach (var plugin in plugins.GetAll())
            {
                if (plugin.IsCorePlugin)
                {
                    currentPlugin++;
                    continue;
                }

                if (plugin.ResourceId == 0)
                {
                    failures["Missing ResourceId"].Add(plugin);
                    currentPlugin++;
                    continue;
                }

                webrequest.Enqueue(PluginInformationUrl.Replace("{resourceId}", plugin.ResourceId.ToString()), null,
                    (code, response) =>
                    {
                        if (code != 200)
                        {
                            PrintWarning($"Failed to access plugin information API at {PluginInformationUrl.Replace("{resourceId}", plugin.ResourceId.ToString())}\nIf this keeps happening, please content the developer.");
                        }
                        else
                        {
                            var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(response);

                            if (!apiResponse.HasSucceeded && apiResponse.Error == "RESOURCE_NOT_AVAILABLE")
                            {
                                failures["Resource Unavailable"].Add(plugin);
                            }
                            // Version is null or empty; Unable to read version
                            else if (string.IsNullOrEmpty(apiResponse.PluginData.Version))
                            {
                                failures["Resource Details Unavailable"].Add(plugin);
                            }
                            else if (IsOutdated(plugin.Version, apiResponse.PluginData.Version))
                            {
                                outdatedPlugins.Add(plugin, apiResponse.PluginData);
                            }
                        }

                        // Reached last plugin
                        if (currentPlugin++ >= totalPlugins)
                        {
                            foreach (var failure in failures)
                            {
                                if (failure.Value.Count == 0)
                                    continue;

                                SendMessage(
                                    requestor,
                                    GetMsg(failure.Key, requestor?.Id)
                                        .Replace("{plugins}", failure.Value.Select(p => p.Name).ToSentence())
                                );
                            }

                            var outdatedPluginText = GetMsg("Outdated Plugin Info");

                            var outdatedPluginLines = outdatedPlugins.Select(kvp =>
                                outdatedPluginText
                                    .Replace("{title}", kvp.Key.Title)
                                    .Replace("{installed}", kvp.Key.Version.ToString())
                                    .Replace("{latest}", kvp.Value.Version).Replace("{url}", kvp.Value.Url)
                            );

                            SendMessage(
                                requestor,
                                GetMsg("Outdated Plugin List")
                                    .Replace("{plugins}", string.Join(Environment.NewLine, outdatedPluginLines.ToArray()))
                            );
                        }

                    }, this);
            }
        }

        #endregion

        #region Version Related

        private bool IsOutdated(Version installed, string latest)
        {
            if (!IsNumeric(latest.Replace(".", string.Empty)))
            {
                return false;
            }

            var latestPartials = latest.Split('.').Select(int.Parse).ToArray();

            return installed < GetVersion(latestPartials);
        }

        private static Version GetVersion(int[] partials)
        {
            if (partials.Length >= 3)
                return new Version(partials[0], partials[1], partials[2]);

            return new Version();
        }

        #endregion

        #region Helper

        private void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null)
                Config.Set(args);
        }

        private T GetConfig<T>(T defaultVal, params string[] args)
        {
            if (Config.Get(args) == null)
            {
                PrintError($"The plugin failed to read something from the config: {string.Join("/", args)}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T) Convert.ChangeType(Config.Get(args), typeof(T));
        }

        private string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID?.ToString());

        private static bool IsNumeric(string text) => !text.Any(c => c < 48 || c > 57);

        #endregion

        #region Classes

        private struct ApiResponse
        {
            [JsonProperty("success")] private bool _hasSucceeded;
            [JsonProperty("data")] private Data _pluginData;
            [JsonProperty("error")] private string _error;

            public bool HasSucceeded => _hasSucceeded;
            public Data PluginData => _pluginData;
            public string Error => _error;

            public struct Data
            {
                [JsonProperty("resourceId")] private int _resourceId;
                [JsonProperty("title")] private string _title;
                [JsonProperty("version")] private string _version;
                [JsonProperty("developer")] private string _developer;
                [JsonProperty("url")] private string _url;

                public int ResourceId => _resourceId;
                public string Title => _title;
                public string Version => _version;
                public string Developer => _developer;
                public string Url => _url;
            }
        }

        #endregion
    }
}
