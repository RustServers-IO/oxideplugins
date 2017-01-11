using System.Collections.Generic;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("AreaNuke", "Noviets", "1.0.3", ResourceId = 1689)]
    [Description("Removes every object within a specified range")]

    class AreaNuke : HurtworldPlugin
    {
		void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"nopermission","AreaNuke: You dont have Permission to do this!"},
                {"invalidrange","AreaNuke: The range must be a number."},
				{"invalidargs","AreaNuke: Incorrect usage: /areanuke [range] (example; /areanuke 5)"},
				{"destroyed","AreaNuke: Destroyed {Count} objects"}
			};
			lang.RegisterMessages(messages, this);
        }
		string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);
		void Loaded()
        {
            permission.RegisterPermission("areanuke.admin", this);
			LoadDefaultMessages();
		}
		
		List<string> destroyedList = new List<string>();
		List<string> erroredList = new List<string>();
		void Save()
		{
			Oxide.Core.Interface.GetMod().DataFileSystem.WriteObject("AreaNuke/DestroyedList", destroyedList);
			Oxide.Core.Interface.GetMod().DataFileSystem.WriteObject("AreaNuke/ErroredList", erroredList);
		}
		
		[ChatCommand("areanuke")]
        void cmdareanuke(PlayerSession session, string command, string[] args)
        {
			if(permission.UserHasPermission(session.SteamId.ToString(),"areanuke.admin") || session.IsAdmin)
			{
				if(args.Length == 1)
				{
					int count = 0;
					int range = 0;
					try{range = Convert.ToInt32(args[0]);}
					catch{
						hurt.SendChatMessage(session, Msg("invalidrange",session.SteamId.ToString()));
						return;
					}
					foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
					{
						if(Vector3.Distance(session.WorldPlayerEntity.transform.position, obj.transform.position) <= range)
						{
							uLink.NetworkView nwv = obj.GetComponent<uLink.NetworkView>();
							if(nwv != null && nwv.isActiveAndEnabled)
							{
								if(obj.name.Contains("(Clone)") && !obj.name.Contains("Player"))// && !obj.name.Contains("Seat"))
								{
									Singleton<HNetworkManager>.Instance.NetDestroy(nwv);
									count++;
								}
							}
						}
					}
					Save();
					hurt.SendChatMessage(session, Msg("destroyed",session.SteamId.ToString()).Replace("{Count}",count.ToString()));
				}
				else if(args.Length == 2)
				{
					int count = 0;
					int range = 0;
					try{range = Convert.ToInt32(args[0]);}
					catch{
						hurt.SendChatMessage(session, Msg("invalidrange",session.SteamId.ToString()));
						return;
					}
					foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
					{
						if(obj.name.ToString().Contains(args[1].ToLower()))
						{
							if(Vector3.Distance(session.WorldPlayerEntity.transform.position, obj.transform.position) <= range)
							{
								uLink.NetworkView nwv = obj.GetComponent<uLink.NetworkView>();
								if(nwv != null && nwv.isActiveAndEnabled)
								{
									if(obj.name.Contains("(Clone)") && !obj.name.Contains("Player"))// && !obj.name.Contains("Seat"))
									{
										Singleton<HNetworkManager>.Instance.NetDestroy(nwv);
										count++;
									}
								}
							}
						}
					}
					hurt.SendChatMessage(session, Msg("destroyed",session.SteamId.ToString()).Replace("{Count}",count.ToString()));
				}
				else
					hurt.SendChatMessage(session, Msg("invalidargs",session.SteamId.ToString()));
			}
			else
				hurt.SendChatMessage(session, Msg("nopermission",session.SteamId.ToString()));
        }
	}
}