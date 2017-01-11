using System;
using System.Collections.Generic;
using System.Linq;
namespace Oxide.Plugins
{
    [Info("Restarter", "OpenFun", 0.3)]
    [Description("Save Restart by OpenFun")]

    class Restarter : SevenDaysPlugin
    {
		void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"RestartMessage", "Server restart in"},
                {"Secounds", "secounds."},
                {"KickMessage", "Restart..."},
            }, this);
        }
		
		void Loaded()
		{
			LoadDefaultMessages();
		}
		
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
		
		public bool AnonceRestart => Config.Get<bool>("Anonce Restart");
		public bool AnoncePrintToChat => Config.Get<bool>("Anonce Print To Chat");
		public int war => Config.Get<int>("Timer Warning Message");
		protected override void LoadDefaultConfig()
        { 
            PrintWarning("Creating a new configuration file.");
            Config.Clear();
            Config["Anonce Restart"] = true;
            Config["Anonce Print To Chat"] = true;
            Config["Timer Warning Message"] = 60;
            SaveConfig();
        }
		
		void OnPlayerChat(ClientInfo _cInfo, string message)
		{
			if (!string.IsNullOrEmpty(message) && message.StartsWith("/") && !string.IsNullOrEmpty(_cInfo.playerName) )
			{
				EntityPlayer _player = GameManager.Instance.World.Players.dict[_cInfo.entityId];
				if ( message.StartsWith("/restart") )
				{
					if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
					{
						int wrn = war;
						timer.Repeat(1, 0, () =>
						{
						wrn = wrn - 1;
						if (AnoncePrintToChat)
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000]{0} {1} {2} [FFFFFF]", GetMessage("RestartMessage", _cInfo.playerId), wrn, GetMessage("Secounds", _cInfo.playerId)), "Server", false, "", false));
						}
							if (wrn == 0)
							{
								SdtdConsole.Instance.ExecuteSync(string.Format("Kickall \"{0}\"", GetMessage("KickMessage", _cInfo.playerId)), _cInfo);
								SdtdConsole.Instance.ExecuteSync("Saveworld", _cInfo);
								if (AnonceRestart)
								{
									PrintWarning("Server restarting...");
								}
							}
							if (wrn == -5)
							{
								SdtdConsole.Instance.ExecuteSync("Shutdown", _cInfo);
							}
							return;
						});
					}
				}	
			}
		}
	}
}