using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Magic Description", "Wulf/lukespragg", "1.3.1", ResourceId = 1447)]
    [Description("Adds dynamic information in the server description")]
    public class MagicDescription : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Server description")]
            public string Description;

            [JsonProperty(PropertyName = "Update interval (seconds)")]
            public int UpdateInterval;

            [JsonProperty(PropertyName = "Show loaded plugins (true/false)")]
            public bool ShowPlugins;

            [JsonProperty(PropertyName = "Hidden plugins (filename or title)")]
            public List<string> HiddenPlugins;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Description = "Powered by Oxide {magic.version} for Rust {magic.version protocol}\\n\\n{server.pve}",
                    UpdateInterval = 300,
                    ShowPlugins = false,
                    HiddenPlugins = new List<string> { "PrivateStuff", "OtherName", "Epic Stuff" }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.Description == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Initialization

        private static readonly Regex varRegex = new Regex(@"\{(.*?)\}");
        private static bool serverInitialized;

        private void OnServerInitialized()
        {
            UpdateDescription();
            serverInitialized = true;
            timer.Every(config.UpdateInterval, () => UpdateDescription());

            if (!config.ShowPlugins) { Unsubscribe(nameof(OnPluginLoaded)); Unsubscribe(nameof(OnPluginUnloaded)); }
        }

        #endregion

        #region Description Handling

        private void OnPluginLoaded()
        {
            if (serverInitialized) UpdateDescription();
        }

        private void OnPluginUnloaded() => UpdateDescription();

        private void UpdateDescription(string text = "")
        {
            if (!string.IsNullOrEmpty(text))
            {
                Config["Description"] = text;
                config.Description = text;
                SaveConfig();
            }

            var newDescription = new StringBuilder(config.Description);

            var matches = varRegex.Matches(config.Description);
            foreach (Match match in matches)
            {
                var command = match.Groups[1].Value;
                if (string.IsNullOrEmpty(command)) continue;

                var reply = ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), command);
                newDescription.Replace(match.ToString(), reply.Replace("\"", "") ?? "");
            }

            if (config.ShowPlugins)
            {
                var loaded = plugins.GetAll();
                if (loaded.Length == 0) return;

                var count = 0;
                string pluginList = null;
                foreach (var plugin in loaded.Where(p => !p.IsCorePlugin))
                {
                    if (config.HiddenPlugins.Contains(plugin.Title) || config.HiddenPlugins.Contains(plugin.Name)) continue;

                    pluginList += plugin.Title + ", ";
                    count++;
                }
                if (pluginList != null)
                {
                    if (pluginList.EndsWith(", ")) pluginList = pluginList.Remove(pluginList.Length - 2);
                    newDescription.Append($"\n\nPlugins ({count}): {pluginList}");
                }
            }

            if (newDescription.ToString() == ConVar.Server.description) return;

            ConVar.Server.description = newDescription.ToString();
            Puts("Server description updated!");
        }

        #endregion

        #region Command Handling

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (!serverInitialized) return null;

            var command = arg.cmd.FullName;
            if (command != "server.description" || !arg.IsAdmin) return null;
            if (!arg.HasArgs() || arg.Args.GetValue(0) == null) return null;

            var newDescription = string.Join(" ", arg.Args.ToArray());
            UpdateDescription(newDescription);
            Interface.Oxide.LogInfo($"server.description: {newDescription}");
            return true;
        }

        [ConsoleCommand("magic.version")]
        private void VersionCommand(ConsoleSystem.Arg arg)
        {
            var oxide = Core.OxideMod.Version.ToString();
            var protocol = Rust.Protocol.printable;
            var branch = Facepunch.BuildInfo.Current.Scm.Branch;
            var date = Facepunch.BuildInfo.Current.BuildDate.ToLocalTime().ToString();

            var args = arg.FullString.ToLower();
            switch(args)
            {
                default:
                case "oxide":
                    arg.ReplyWith(oxide);
                    break;
                
                case "rust":
                case "protocol":
                    arg.ReplyWith(protocol);
                    break;

                case "branch":
                    arg.ReplyWith(branch);
                    break;

                case "date":
                case "builddate":
                    arg.ReplyWith(date);
                    break;
            }
        }

        #endregion
    }
}
