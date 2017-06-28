using Oxide.Core;
using System;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Clan Tags", "GreenArrow", "0.1")]
    [Description("Adding support for Clan tags in Better Chat. ")]

    class ClanTags : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat,Clans,HWClans;
		
		object defaultClanCol,before,after;

        protected override void LoadDefaultConfig() {
			Config.Clear();
            Config["defaultClanCol"] = "FFA500";
            Config["BeforeTag"] = "[";
            Config["AfterTag"] = "]";
		    SaveConfig();
		}
	    
	
        private string GetUsersClan(IPlayer player) {
#if RUST
    		string clan = (string)Clans?.Call("GetClanOf",player.Object);
#endif
#if HURTWORLD
	        string clan = (string)HWClans?.Call("getClanTag_bySession",player.Object);
#endif
			if (clan != null)
			    return clan;
		
		    return null;
		}
		
        private string GetClanTagFormatted(IPlayer player)
        {	

			string clantag = GetUsersClan(player);
			
            string togetherstring = covalence.FormatText($"[#{defaultClanCol}]{before}{clantag}{after}[/#]");
			
            if (clantag != null && !string.IsNullOrEmpty(clantag))
                return togetherstring;

            return null;
        }
		
		private void OnPluginLoaded(Plugin plugin)
		{
			if (plugin.Title != "Better Chat")
				return;

    		Interface.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(GetClanTagFormatted));

		}
		
        void OnServerInitialized()
		{
        
    		defaultClanCol = Config["defaultClanCol"];
            before = Config["BeforeTag"];
		    after = Config["AfterTag"];
			
    		BetterChat?.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(GetClanTagFormatted));

		}
		
	}
}
