using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("AvtoSaveWorld", "OpenFun", 0.1)]
    [Description("AvtoSaveWorld by OpenFun")]
	
    class AvtoSaveWorld : SevenDaysPlugin
    {
	public bool AnonceSave => Config.Get<bool>("Anonce Save");
	public bool AnonceNoPlayers => Config.Get<bool>("Anonce No Players");
	public bool PrintMessageToChat => Config.Get<bool>("Print Message To Chat");
	public int TimerInSecounds => Config.Get<int>("Timer In Secounds");
		protected override void LoadDefaultConfig()
        { 
            PrintWarning("Creating a new configuration file.");
            Config.Clear();
            Config["Anonce Save"] = true;
            Config["Anonce No Players"] = true;
			Config["Print Message To Chat"] = false;
            Config["Timer In Secounds"] = 600;
            SaveConfig();
        }		
        void Init()
        { 
			timer.Repeat(TimerInSecounds, 0, () =>
			{
				int _playerCount = ConnectionManager.Instance.ClientCount();
				if (_playerCount > 0)
				{
					List<ClientInfo> _cInfoList = ConnectionManager.Instance.GetClients();
					ClientInfo _cInfo = _cInfoList.RandomObject();
					SdtdConsole.Instance.ExecuteSync("saveworld", _cInfo);
					if(AnonceSave)
					{
						Puts("Try to save Game World...");
					}
					if(PrintMessageToChat)
					{
						PrintToChat("Saving World...");
					}
				}
				else
				{
					if(AnonceNoPlayers)
					{
						PrintWarning("No Players Online...");
					}
				}
			});
        }
    }
}