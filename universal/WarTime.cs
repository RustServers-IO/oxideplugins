using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events.Entities;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WarTime", "D-Kay", "1.1.2", ResourceId = 1012)]
    public class WarTime : ReignOfKingsPlugin {

        #region Configuration Data

        bool noSpam = false;

        private bool ignoreBallista => GetConfig("ignoreBallista", false);

        private bool ignoreTrebuchet => GetConfig("ignoreTrebuchet", false);

        private bool adminSiegeException => GetConfig("adminSiegeException", false);

        private int banTime => GetConfig("BanTime", 1);

        private int Peacetime => GetConfig("Peacetime", 23);

        private string punish => GetConfig("Punish", "ban");

        private bool usingRealTime => GetConfig("UsingRealtime", false);

        private int Wartime => GetConfig("Wartime", 9);

        private bool warOn = false;

        #endregion

        #region Config save/load

        private void SaveWartimeData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SavedWartimeState", warOn);
        }

        private void LoadWartimeData()
        {
            warOn = Interface.GetMod().DataFileSystem.ReadObject<bool>("SavedWartimeState");
        }

        private void Loaded()
        {
            LoadDefaultMessages();
            LoadWartimeData();
            permission.RegisterPermission("WarTime.Toggle", this);
            permission.RegisterPermission("WarTime.Exception", this);
            if (usingRealTime) timer.Repeat(1, 0, CheckTime);
        }

        protected override void LoadDefaultConfig()
        {
            Config["ignoreBallista"] = ignoreBallista;
            Config["ignoreTrebuchet"] = ignoreTrebuchet;
            Config["adminSiegeException"] = adminSiegeException;
            Config["BanTime"] = banTime;
            Config["Peacetime"] = Peacetime;
            Config["Punish"] = punish;
            Config["UsingRealtime"] = usingRealTime;
            Config["Wartime"] = Wartime;
            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "ChatPrefix", "[950415]WarTime[ffffff]: {0}" },
                { "PeaceTime", "It is now a time of Peace! Do not siege!" },
                { "WarTime", "It is now a time of War! You may now siege!" },
                { "SiegeUsed", "A player used a siege weapon during Peace Times! The player was kicked from the server!" },
                { "BaseSieged", "A base was sieged during Peace Time. The attacker was kicked from the server!" },
                { "PunishReason", "Sieging during Peace Times!" },
                { "ToggleWhileUsingRealTime", "Toggle is disabled while RealTime is being used." },
                { "HelpTitle", "[0000FF]Wartime Command[FFFFFF]" },
                { "HelpWartime", "[00FF00]/wartime[FFFFFF] - Toggle between wartime and peacetime." },
                { "HelpCheckWartime", "[00FF00]/checkwartime[FFFFFF] - Shows whether it's wartime or peacetime." }
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("wartime")]
        private void WarTimeCommand(Player player)
        {
			if (player.HasPermission("WarTime.Toggle")) {
                if (usingRealTime) { PrintToChat(player, string.Format(GetMessage("ChatPrefix", player.Id.ToString()), GetMessage("ToggleWhileUsingRealTime", player.Id.ToString()))); return; }
                if (warOn) { PrintToChat(string.Format(GetMessage("ChatPrefix", player.Id.ToString()), GetMessage("PeaceTime", player.Id.ToString()))); warOn = false; }
                else { PrintToChat(string.Format(GetMessage("ChatPrefix", player.Id.ToString()), GetMessage("WarTime", player.Id.ToString()))); warOn = true; }
                SaveWartimeData();
			}
        }
		
		[ChatCommand("checkwartime")]
        private void CheckWarTimeCommand(Player player)
        {
			if (warOn) PrintToChat(player, string.Format(GetMessage("ChatPrefix", player.Id.ToString()), GetMessage("WarTime", player.Id.ToString()))); 
            else PrintToChat(player, string.Format(GetMessage("ChatPrefix", player.Id.ToString()), GetMessage("PeaceTime", player.Id.ToString())));
        }

        #endregion

        #region Hooks

        private void OnCubeTakeDamage(CubeDamageEvent e)
        {
            if (noSpam) return;
            string damageSource = e.Damage.Damager.name.ToString();
            if (!warOn)
            {
                if (e.Damage.DamageSource.Owner is Player)
                {
                    Player player = e.Damage.DamageSource.Owner;
                    if (adminSiegeException && player.HasPermission("WarTime.Exception"))  return;
                    bool trebuchet = false;
                    bool ballista = false;
                    if (!ignoreBallista) ballista = damageSource.Contains("Ballista");
                    if (!ignoreTrebuchet) trebuchet = damageSource.Contains("Trebuchet");
                    if (trebuchet || ballista)
                    {
                        e.Cancel(GetMessage("PunishReason"));
                        e.Damage.Amount = 0f;
                        if (punish == "kick") Server.Kick(player, GetMessage("PunishReason"));
                        if (punish == "ban") Server.Ban(player, banTime, GetMessage("PunishReason"));
                        PrintToChat(GetMessage("ChatPrefix") + GetMessage("BaseSieged"));
                        noSpam = true;
                        timer.In(5, resetNoSpam);
                    }
                }
            }
        }

        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            if (e == null) return;
            if (e.Damage == null) return;
            if (e.Damage.DamageSource == null) return;
            if (e.Damage.Damager == null) return;
            if (e.Entity == null) return;
            if (e.Entity == e.Damage.DamageSource) return;

            string damageSource = e.Damage.Damager.name.ToString();
            if (!warOn)
            {
                if (e.Damage.DamageSource.IsPlayer && !(e.Entity.IsPlayer))
                {
                    if (e.Damage.DamageSource.Owner == null) return;
                    Player player = e.Damage.DamageSource.Owner;
                    if (adminSiegeException && player.HasPermission("WarTime.Exception")) return;
                    if (damageSource.Contains("Trebuchet") || damageSource.Contains("Ballista"))
                    {
                        e.Cancel(GetMessage("PunishReason"));
                        e.Damage.Amount = 0f;
                        PrintToChat(GetMessage("ChatPrefix") + GetMessage("SiegeUsed"));
                        if (punish == "kick") Server.Kick(player, GetMessage("PunishReason"));
                        if (punish == "ban") Server.Ban(player, banTime, GetMessage("PunishReason"));
                    }
                }
            }
        }

        private void SendHelpText(Player player)
        {
            PrintToChat(player, GetMessage("HelpTitle", player.Id.ToString()));
            PrintToChat(player, GetMessage("HelpCheckWartime", player.Id.ToString()));
            if (player.HasPermission("WarTime.Toggle")) PrintToChat(player, GetMessage("HelpWartime", player.Id.ToString()));
        }

        #endregion

        #region Functions

        private void CheckTime()
        {
            bool Check = warOn;
            if (DateTime.Now.Hour >= Peacetime || DateTime.Now.Hour < Wartime)
            {
                warOn = false;
                if (warOn != Check) PrintToChat(GetMessage("PeaceTime"));
            }
            else
            {
                warOn = true;
                if (warOn != Check) PrintToChat(GetMessage("WarTime"));
            }
            SaveWartimeData();
        }

        private void resetNoSpam()
        {
            noSpam = false;
        }

        #endregion

        #region Helpers

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}
