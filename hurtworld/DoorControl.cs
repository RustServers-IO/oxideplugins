// Reference: UnityEngine.UI
using Oxide.Core;
using System;
using System.Collections.Generic;
using uLink;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("DoorControl", "Noviets", "1.0.1")]
    [Description("Open and close all doors or within a specific distance")]

    class DoorControl : HurtworldPlugin
    {
		void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"nopermission","DoorControl: You dont have Permission to do this!"},
                {"opened","DoorControl: Doors have been Opened."},
				{"doorscount", "DoorControl: Single: {Single} Double: {Double} Garage: {Garage}"},
				{"closed","DoorControl: Doors have been Closed."},
				{"invaliddistance","DoorControl: Distance must be a number if you supply one. (No distance will control all doors)"}
            };
			
			lang.RegisterMessages(messages, this);
        }
		protected override void LoadDefaultConfig()
        {
			if(Config["ShowDoorsCount"] == null) Config.Set("ShowDoorsCount", true);
		}
		string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);
		void Loaded()
        {
			permission.RegisterPermission("doorcontrol.admin", this);
			LoadDefaultMessages();
			LoadDefaultConfig();
		}
		
		[ChatCommand("doors")]
        void doorCommand(PlayerSession session, string command, string[] args)
        {
			if(permission.UserHasPermission(session.SteamId.ToString(),"doorcontrol.admin") || session.IsAdmin)
			{
				int i = 0;
				int distance = 0;
				if(args.Length == 2)
				{
					try{distance = Convert.ToInt32(args[1]);}
					catch
					{
						hurt.SendChatMessage(session, Msg("invaliddistance",session.SteamId.ToString()));
						return;
					}
				}
				switch (args[0])
				{
					case "open":
						Open(session, distance);
						break;
					case "close":
						Close(session, distance);
						break;
				}
			}
			else
				hurt.SendChatMessage(session, Msg("nopermission",session.SteamId.ToString()));
		}
		void Open(PlayerSession session, int distance)
		{
			int sdoor = 0;
			int ddoor = 0;
			int gdoor = 0;
			foreach(DoubleDoorServer door in Resources.FindObjectsOfTypeAll<DoubleDoorServer>())
			{
				if (distance > 0)
				{
					if(Vector3.Distance(session.WorldPlayerEntity.transform.position, door.transform.position) <= distance)
					{
						if(!door.IsOpen)
						{
							door.DoorCollider.enabled = false;
							door.RPC("DOP", uLink.RPCMode.OthersBuffered, true);
							door.IsOpen=true;
							ddoor++;
						}
					}
				}
				else
				{
					if(!door.IsOpen)
					{
						door.DoorCollider.enabled = false;
						door.RPC("DOP", uLink.RPCMode.OthersBuffered, true);
						door.IsOpen=true;
						ddoor++;
					}
				}
			}
			foreach(GarageDoorServer door in Resources.FindObjectsOfTypeAll<GarageDoorServer>())
			{
				if (distance > 0)
				{
					if(Vector3.Distance(session.WorldPlayerEntity.transform.position, door.transform.position) <= distance)
					{
						if(!door.IsOpen)
						{
							door.DoorCollider.enabled = false;
							door.RPC("DOP", uLink.RPCMode.OthersBuffered, true);
							door.IsOpen=true;
							gdoor++;
						}
					}
				}
				else
				{
					if(!door.IsOpen)
					{
						door.DoorCollider.enabled = false;
						door.RPC("DOP", uLink.RPCMode.OthersBuffered, true);
						door.IsOpen=true;
						gdoor++;
					}
				}
			}
			foreach(DoorSingleServer door in Resources.FindObjectsOfTypeAll<DoorSingleServer>())
			{
				if (distance > 0)
				{
					if(Vector3.Distance(session.WorldPlayerEntity.transform.position, door.transform.position) <= distance)
					{
						if(!door.IsOpen)
						{
							door.DoorCollider.enabled = false;
							door.RPC("DOP", uLink.RPCMode.OthersBuffered, true);
							door.IsOpen=true;
							sdoor++;
						}
					}
				}
				else
				{
					if(!door.IsOpen)
					{
						door.DoorCollider.enabled = false;
						door.RPC("DOP", uLink.RPCMode.OthersBuffered, true);
						door.IsOpen=true;
						sdoor++;
					}
				}
			}
			hurt.SendChatMessage(session, Msg("opened",session.SteamId.ToString()));
			if ((bool) Config["ShowDoorsCount"])
				hurt.SendChatMessage(session, Msg("doorscount",session.SteamId.ToString()).Replace("{Single}",sdoor.ToString()).Replace("{Double}",ddoor.ToString()).Replace("{Garage}",gdoor.ToString()));
			
		}
		void Close(PlayerSession session, int distance)
		{
			int sdoor = 0;
			int ddoor = 0;
			int gdoor = 0;
			foreach(DoubleDoorServer door in Resources.FindObjectsOfTypeAll<DoubleDoorServer>())
			{
				if (distance > 0)
				{
					if(Vector3.Distance(session.WorldPlayerEntity.transform.position, door.transform.position) <= distance)
					{
						if(door.IsOpen)
						{
							door.DoorCollider.enabled = true;
							door.RPC("DOP", uLink.RPCMode.OthersBuffered, false);
							door.IsOpen=false;
							ddoor++;
						}
					}
				}
				else
				{
					if(door.IsOpen)
					{
						door.DoorCollider.enabled = true;
						door.RPC("DOP", uLink.RPCMode.OthersBuffered, false);
						door.IsOpen=false;
						ddoor++;
					}
				}
			}
			foreach(GarageDoorServer door in Resources.FindObjectsOfTypeAll<GarageDoorServer>())
			{
				if (distance > 0)
				{
					if(Vector3.Distance(session.WorldPlayerEntity.transform.position, door.transform.position) <= distance)
					{
						if(door.IsOpen)
						{
							door.DoorCollider.enabled = true;
							door.RPC("DOP", uLink.RPCMode.OthersBuffered, false);
							door.IsOpen=false;
							gdoor++;
						}
					}
				}
				else
				{
					if(door.IsOpen)
					{
						door.DoorCollider.enabled = true;
						door.RPC("DOP", uLink.RPCMode.OthersBuffered, false);
						door.IsOpen=false;
						gdoor++;
					}
				}
			}
			foreach(DoorSingleServer door in Resources.FindObjectsOfTypeAll<DoorSingleServer>())
			{
				if (distance > 0)
				{
					if(Vector3.Distance(session.WorldPlayerEntity.transform.position, door.transform.position) <= distance)
					{
						if(door.IsOpen)
						{
							door.DoorCollider.enabled = true;
							door.RPC("DOP", uLink.RPCMode.OthersBuffered, false);
							door.IsOpen=false;
							sdoor++;
						}
					}
				}
				else
				{
					if(door.IsOpen)
					{
						door.DoorCollider.enabled = true;
						door.RPC("DOP", uLink.RPCMode.OthersBuffered, false);
						door.IsOpen=false;
						sdoor++;
					}
				}
			}
			hurt.SendChatMessage(session, Msg("closed",session.SteamId.ToString()));
			if ((bool) Config["ShowDoorsCount"])
				hurt.SendChatMessage(session, Msg("doorscount",session.SteamId.ToString()).Replace("{Single}",sdoor.ToString()).Replace("{Double}",ddoor.ToString()).Replace("{Garage}",gdoor.ToString()));
		}
	}
}