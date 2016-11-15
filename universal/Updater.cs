using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("Updater", "LaserHydra", "2.1.1", ResourceId = 681)]
    [Description("Notifies you if you have outdated plugins.")]
    internal class Updater : CovalencePlugin
    {
        #region Global Declaration

        private readonly Dictionary<Plugin, string> outdatedPlugins = new Dictionary<Plugin, string>();
        private readonly List<Plugin> missingResourceID = new List<Plugin>();

        [PluginReference("EmailAPI")]
        private Plugin EmailAPI;

        [PluginReference("PushAPI")]
        private Plugin PushAPI;

        #endregion

        #region Hooks

        private void Loaded()
        {
            LoadConfig();
            LoadMessages();

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

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Checking", "Checking for updates... This may take a few seconds. Please be patient."},
                {"Outdated Plugin List", "Following plugins are outdated: {plugins}"},
                {"Outdated Plugin Info", "# {title} | Installed: {installed} - Latest: {latest} | {url}"},
                {"ResourceID Not Set", "Couldn't check for updates of following plugins as they have no ResourceID set: {plugins}"},
                {"Failed To Get Version", "Couldn't get the latest version of plugin {title}"},
            }, this);
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        #endregion

        #region Commands

        [Command("updates"), Permission("updater.check")]
        private void cmdUpdates(IPlayer player, string cmd, string[] args)
        {
            SendMessage(player, GetMsg("Checking", player.Id));
            CheckForUpdates(player);
        }

        #endregion

        #region Subject Related

        private void SendMessage(IPlayer player, string message)
        {
            if (player != null)
                player.Reply(message);
            else
                PrintWarning(message);
        }

        private void Notify(IPlayer player, string message)
        {
            if (player == null && GetConfig(false, "Settings", "Use PushAPI") && PushAPI != null)
                PushAPI.Call("PushMessage", "Plugin Update Notification", message);

            if (player == null && GetConfig(false, "Settings", "Use EmailAPI") && EmailAPI != null)
                EmailAPI.Call("EmailMessage", "Plugin Update Notification", message);

            SendMessage(player, message);
        }

        private void CheckForUpdates(IPlayer player)
        {
            outdatedPlugins.Clear();
            missingResourceID.Clear();

            int pluginCount = 0;
            string playerId = player?.Id ?? "0";

            foreach (var plugin in plugins.GetAll())
            {
                if (plugin.IsCorePlugin)
                {
                    pluginCount++;

                    if (pluginCount == plugins.GetAll().Length)
                        NotifyOutdated(player);

                    continue;
                }

                if (plugin.ResourceId == 0)
                {
                    missingResourceID.Add(plugin);
                    
                    pluginCount++;

                    if (pluginCount == plugins.GetAll().Length)
                        NotifyOutdated(player);

                    continue;
                }

                webrequest.EnqueueGet($"http://oxidemod.org/plugins/{plugin.ResourceId}/", (code, response) =>
                {
                    pluginCount++;

                    if (code == 200 && response != null)
                    {
                        string latest = "0.0.0.0";

                        Match version = new Regex(@"<h3>Version (\d{1,7}(\.\d{1,7})+?)<\/h3>").Match(response);

                        if (version.Success)
                        {
                            latest = version.Groups[1].ToString();

                            if (IsOutdated(plugin.Version.ToString(), latest))
                                outdatedPlugins.Add(plugin, latest);

                            if (pluginCount == plugins.GetAll().Length)
                                NotifyOutdated(player);
                        }
                        else
                        {
                            SendMessage(player, GetMsg("Failed To Get Version", playerId).Replace("{title}", plugin.Title));

                            if (pluginCount == plugins.GetAll().Length)
                                NotifyOutdated(player);
                        }
                    }
                    else
                    {
                        SendMessage(player, GetMsg("Failed To Get Version", playerId).Replace("{title}", plugin.Title));

                        if (pluginCount == plugins.GetAll().Length)
                            NotifyOutdated(player);
                    }
                }, this, null, 10f);
            }
        }

        private void NotifyOutdated(IPlayer player)
        {
            if (missingResourceID.Count != 0)
            {
                string plugins = string.Join(", ", (from p in missingResourceID select p.Title).ToArray());

                Notify(player, GetMsg("ResourceID Not Set", player?.Id ?? "0").Replace("{plugins}", plugins));
            }

            if (outdatedPlugins.Count != 0)
            {
                string message = Environment.NewLine +
                                    GetMsg("Outdated Plugin List").Replace("{plugins}", Environment.NewLine + string.Join(Environment.NewLine, (from kvp in outdatedPlugins select Environment.NewLine + GetMsg("Outdated Plugin Info").Replace("{title}", kvp.Key.Title).Replace("{installed}", kvp.Key.Version.ToString()).Replace("{latest}", kvp.Value).Replace("{url}", $"http://oxidemod.org/plugins/{kvp.Key.ResourceId}/")).ToArray()) +
                                    Environment.NewLine);

                Notify(player, message);
            }

            outdatedPlugins.Clear();
        }

        private bool IsOutdated(string installed, string latest)
        {
            char[] chars = "1234567890.".ToCharArray();

            foreach (char Char in installed.ToCharArray())
                if (!chars.Contains(Char))
                    installed = installed.Replace(Char.ToString(), "");

            foreach (char Char in latest.ToCharArray())
                if (!chars.Contains(Char))
                    latest = latest.Replace(Char.ToString(), "");

            int[] installedArray = (from v in installed.Split('.') select Convert.ToInt32(v)).ToArray();
            int[] latestArray = (from v in latest.Split('.') select Convert.ToInt32(v)).ToArray();

            int i = 0;
            foreach(int lst in latestArray)
            {
                int inst = installedArray.Count() - 1 >= i ? installedArray[i] : 0;

                if (lst > inst)
                    return true;
                else if (lst < inst)
                    return false;

                i++;
            }

            return false;
        }

        #endregion

        #region General

        #region Config Helper

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

        #endregion

        #region Message Helper

        private string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());

        #endregion
        
        #endregion
    }
}