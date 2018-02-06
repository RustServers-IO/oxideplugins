using Oxide.Core;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoFallDmg", "Noviets", "2.0.1", ResourceId = 1821)]
    [Description("Disables fall damage")]

    class NoFallDmg : HurtworldPlugin
    {
		void Loaded() 
		{
			permission.RegisterPermission("nofalldmg.admin", this);
			LoadDefaultMessages();
			UpdateAllPlayers();
		}
		void UpdateAllPlayers()
		{
			foreach(PlayerSession session in GameManager.Instance.GetSessions().Values)
			{
				if(session != null && session.IsLoaded)
				{
					NFD(session);
				}
			}
		}
		protected override void LoadDefaultConfig()
        {
			if(Config["NoFallDmgEnabled"] == null) Config.Set("NoFallDmgEnabled", true);
			SaveConfig();
		}
		void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"nopermission","NoFallDmg: You do not have Permission to do this!"},
                {"nodamagesuccess","NoFallDmg: No Fall Damage: You now have No Fall Damage"},
				{"nodamagetoggle","NoFallDmg: All Fall Damage is now: {Mode}"},
				{"error","NoFallDmg: Error, that is not a valid command."}
            };
			
			lang.RegisterMessages(messages, this);
        }
		string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);
		void OnPlayerSpawn(PlayerSession session)
		{
			NFD(session);
		}
		void OnPlayerRespawn(PlayerSession session)
		{
			NFD(session);
		}
		void NFD(PlayerSession session)
		{
			CharacterMotorSimple motor = session.WorldPlayerEntity.GetComponent<CharacterMotorSimple>();
			if((bool)Config["NoFallDmgEnabled"])
				motor.FallDamageMultiplier = 0f;
			else
				motor.FallDamageMultiplier = 1.5f;
		}
		[ChatCommand("nfd")]
		void nfd(PlayerSession session, string command, string[] args)
		{
			if(permission.UserHasPermission(session.SteamId.ToString(),"nofalldmg.admin") || session.IsAdmin)
			{
				if(args.Length == 0)
				{
					CharacterMotorSimple motor = session.WorldPlayerEntity.GetComponent<CharacterMotorSimple>();
					motor.FallDamageMultiplier = 0f;
					hurt.SendChatMessage(session, Msg("nodamagesuccess",session.SteamId.ToString()));
				}
				else if(args.Length == 1)
				{
					if(args[0].ToLower() == "enabled" || args[0].ToLower() == "on")
					{
						Config.Set("NoFallDmgEnabled", true);
						hurt.SendChatMessage(session, Msg("nodamagetoggle",session.SteamId.ToString()).Replace("{Mode}", "Enabled"));
						UpdateAllPlayers();
					}
					else if(args[0].ToLower() == "disabled" || args[0].ToLower() == "off")
					{
						Config.Set("NoFallDmgEnabled", false);
						hurt.SendChatMessage(session, Msg("nodamagetoggle",session.SteamId.ToString()).Replace("{Mode}", "Disabled"));
						UpdateAllPlayers();
					}
					else
						hurt.SendChatMessage(session, Msg("error",session.SteamId.ToString()));
				}
				else
					hurt.SendChatMessage(session, Msg("error",session.SteamId.ToString()));
			}
			else
				hurt.SendChatMessage(session, Msg("nopermission",session.SteamId.ToString()));
		}
	}
}