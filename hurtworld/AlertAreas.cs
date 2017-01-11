using Oxide.Core;
using Oxide.Core.Plugins;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AlertAreas", "ALEX_and_ER", "0.1.1", ResourceId = 1749)]
    class AlertAreas : HurtworldPlugin
    {
		static string msgPrefixColor = "orange";
		static string msgPrefix	= "<color="+ msgPrefixColor +">[Alert Areas]</color>";
		
		List<AlertArea> Areas = new List<AlertArea>();
		string editAreaName = null;

		class AlertArea
		{
			public string name { get; set; }
			public string alertText { get; set; }
			public float firstCornerX { get; set; }
			public float firstCornerZ { get; set; }
			public float secondCornerX { get; set; }
			public float secondCornerZ { get; set; }
			public bool constant { get; set; }

			private HashSet<string> received = new HashSet<string>();

			public void AddReceived(string steamId)
			{
				this.received.Add(steamId);
			}

			public void RemoveReceived(string steamId)
			{
				this.received.Remove(steamId);
			}

			public void ClearReceived()
			{
				this.received.Clear();
			}

			public bool IsReceived(string steamId)
			{
				return this.received.Contains(steamId);
			}

			public AlertArea() {}

			public AlertArea(string name, bool constant = false)
			{
				this.name = name;
				this.constant = constant;
			}

			public AlertArea(string name, string alertText, float firstCornerX, float firstCornerZ, float secondCornerX, float secondCornerZ, bool constant = false)
			{
				this.name = name;
				this.alertText = alertText;
				this.firstCornerX = firstCornerX;
				this.firstCornerZ = firstCornerZ;
				this.secondCornerX = secondCornerX;
				this.secondCornerZ = secondCornerZ;
				this.constant = constant;
			}
		}

		private void LoadAlertAreas()
        {
            var _Areas = Interface.GetMod().DataFileSystem.ReadObject<Collection<AlertArea>>("AlertAreas");
            foreach (var item in _Areas)
            {
                Areas.Add(new AlertArea(
						item.name,
						item.alertText,
						item.firstCornerX,
						item.firstCornerZ,
						item.secondCornerX,
						item.secondCornerZ,
						item.constant
					));
            }
        }

        private void SaveAlertAreas()
        {
            Interface.GetMod().DataFileSystem.WriteObject("AlertAreas", Areas);
        }

        void Loaded()
        {
            LoadMessages();
            LoadAlertAreas();
        }

		bool IsAlertArea(Vector3 position, AlertArea area) {

			float minX = area.firstCornerX < area.secondCornerX ? area.firstCornerX : area.secondCornerX;
			float maxX = area.secondCornerX > area.firstCornerX ? area.secondCornerX : area.firstCornerX;

			float minZ = area.firstCornerZ < area.secondCornerZ ? area.firstCornerZ : area.secondCornerZ;
			float maxZ = area.secondCornerZ > area.firstCornerZ ? area.secondCornerZ : area.firstCornerZ;

			if(minX <= position.x && position.x <= maxX
			&& minZ <= position.z && position.z <= maxZ) {
				return true;
			}

			return false;
		}
		
		// For TradeZone plugin by SouZa.
        bool isInsideArea(Vector3 position, string areaName)
        {
            foreach (AlertArea area in Areas) {
                if (area.name.ToLower().Contains(areaName.ToLower())) {
                    if (IsAlertArea(position, area)) {
						return true;
					}
                }
            }
            return false;
        }

		void SendAreaAlerts(PlayerSession session)
		{
			//Puts("Checking area '" + area.name + "' for SteamId: ");
			string steamId = session.SteamId.ToString();

			if(!String.IsNullOrEmpty(steamId)) {
				foreach(AlertArea area in Areas) {
					
					Vector3 playerPosition = new Vector3(
							session.WorldPlayerEntity.transform.position.x, 
							session.WorldPlayerEntity.transform.position.y, 
							session.WorldPlayerEntity.transform.position.z
						);
					
					if(IsAlertArea(playerPosition, area) && !string.IsNullOrEmpty(area.alertText) && (!area.IsReceived(steamId) || area.constant)) {
						AlertManager.Instance.GenericTextNotificationServer(area.alertText, session.Player);
						area.AddReceived(steamId);
					}
					else {
						area.RemoveReceived(steamId);
					}
				}
			}
		}

		void OnPlayerInput(PlayerSession session, InputControls input)
        {
			if(input.Forward
			|| input.Backward
			|| input.StrafeLeft
			|| input.StrafeRight
			|| input.Sprint
			|| input.Crouch)
			{
				SendAreaAlerts(session);
			}
        }

		bool HasAccess(PlayerSession session) => session.IsAdmin;

		void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"msg_noPermission", "You dont have permission to do this!"},
                {"msg_wrongSyntaxEditName", "Wrong syntax! Use: <color=aqua>/alertareas name <NewAreaName></color>"},
                {"msg_editNameSuccess", "Now area has the new name <color=green>{newName}</color>"},
                {"msg_wrongSyntaxMain", "Wrong syntax! Use: <color=aqua>/alertareas help</color>"},
				{"msg_help", "Available commands:"},
				{"msg_helpList", "<color=aqua>/alertareas list</color> - show all areas with alert."},
				{"msg_helpAdd", "<color=aqua>/alertareas add <AreaName> [true]</color> - add new area (with optional \"true\" alert text will be shown constant)."},
				{"msg_helpRemove", "<color=aqua>/alertareas remove <AreaName></color> - remove area (case insensitive)."},
				{"msg_helpEdit", "<color=aqua>/alertareas edit <AreaName></color> - edit mode where you can set alert text, corners coordinates and 'constant' attribute."},
				{"msg_noAlertAreas", "No areas with alert."},
                {"msg_areaListHeader", "Areas with alerts:"},
				{"msg_areaListItemTitle", "{i}. <color=green>{name}</color>: {alertText}"},
				{"msg_areaListItemCoords", "<color=grey>({firstCornerCoords}|{secondCornerCoords}) {constant}</color>"},
				{"msg_areaListItemConstant", "<color=red>(constant)</color>"},
                {"msg_addWrongSyntax", "Wrong syntax! Use: <color=aqua>/alertareas add <AreaName> [true]</color>"},
				{"msg_editWrongSyntax", "Wrong syntax! Use: <color=aqua>/alertareas edit <AreaName></color>"},
				{"msg_removeWrongSyntax", "Wrong syntax! Use: <color=aqua>/alertareas remove <AreaName></color>"},
                {"msg_constant", "constant"},
                {"msg_notConstant", "not constant"},
				{"msg_editCornerSuccess", "Corner {n} was successfully saved for area <color=green>{name}</color>."},
				{"msg_editTextSuccess", "Alert text was successfully saved for area <color=green>{name}</color>."},
				{"msg_editConstantSuccess", "Now the alert of area <color=green>{name}</color> is <color=green> {constantOrNot}</color>."},
				{"msg_wrongSyntaxEditCorner", "Wrong syntax! Use: <color=aqua>/alertareas corner 1</color> to set first corner and <color=aqua>/alertareas corner 2</color> to set second corner."},
				{"msg_wrongSyntaxEditConstant", "Wrong syntax! Use: <color=aqua>/alertareas constant <true|false></color>"},
				{"msg_wrongSyntaxEditText", "Wrong syntax! Use: <color=aqua>/alertareas text <Some alert text!></color>"},
				{"msg_areaNotFound", "Area <color=green>{name}</color> not found."},
                {"msg_editAreaNotFound", "Editing area <color=green>{name}</color> not found."},
                {"msg_areaAdded", "Area <color=green>{name}</color> has been added."},
                {"msg_areaRemoved", "Area <color=green>{name}</color> has been removed."},
                {"msg_areaAlreadyExists", "Area <color=green>{name}</color> already exists (case insensitive)."},
				{"msg_editMode", "Now you are editing area <color=green>{name}</color>"},
				{"msg_helpEditCorner1", "<color=aqua>/alertareas corner 1</color> - set first corner coordinates."},
				{"msg_helpEditCorner2", "<color=aqua>/alertareas corner 2</color> - set second corner coordinates."},
				{"msg_helpEditName", "<color=aqua>/alertareas name <NewAreaName></color> - change area name."},
                {"msg_helpEditText", "<color=aqua>/alertareas text <Some alert text!></color> - set area alert text."},
				{"msg_helpEditConstant", "<color=aqua>/alertareas constant <true|false></color> - show alert constant or not."},
                {"msg_notEditMode", "You are not in edit mode. Use: <color=aqua>/alertareas edit <AreaName></color> first."},
            }, this);
        }

		[ChatCommand("alertareas")]
        void cmdAlertAreas(PlayerSession session, string command, string[] args)
        {
			if (!HasAccess(session)) {
				hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_noPermission", session));
				return;
			}

			if (args.Length == 0)
            {
				hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_wrongSyntaxMain", session));
				return;
            }

			string areaName;
			int areaIndex;

			switch (args[0])
			{
				case "help":
					hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_help", session));
					hurt.SendChatMessage(session, GetMsg("msg_helpList", session));
					hurt.SendChatMessage(session, GetMsg("msg_helpAdd", session));
					hurt.SendChatMessage(session, GetMsg("msg_helpRemove", session));
					hurt.SendChatMessage(session, GetMsg("msg_helpEdit", session));
					return;
					
				case "list":
					if(Areas.Count == 0) {
						hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_noAlertAreas", session));
						return;
					}

					hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_areaListHeader", session));

					int rowNum = 0;
					foreach (var area in Areas)
					{
						rowNum++;
						
						hurt.SendChatMessage(session, GetMsg("msg_areaListItemTitle", session)
							.Replace("{i}", rowNum.ToString())
							.Replace("{name}", area.name)
							.Replace("{alertText}", area.alertText)
						);
						
						hurt.SendChatMessage(session, GetMsg("msg_areaListItemCoords", session)
							.Replace("{firstCornerCoords}", area.firstCornerX +","+ area.firstCornerZ)
							.Replace("{secondCornerCoords}", area.secondCornerX +","+ area.secondCornerZ)
							.Replace("{constant}", area.constant ? GetMsg("msg_areaListItemConstant", session) : "")
						);
					}

					break;


				case "add":
				case "edit":
				case "remove":

					if (args.Length < 2)
					{
						hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_" + args[0] + "WrongSyntax", session));
						return;
					}

					editAreaName = null;
					areaName = args[1];
					areaIndex =	Areas.FindIndex(item => item.name.ToLower() == areaName.ToLower());

					switch(args[0]) {

						case "add":
							if (areaIndex >= 0)
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_areaAlreadyExists", session)
									.Replace("{name}", Areas[areaIndex].name));
								return;
							}

							if ((args.Length != 2 && args.Length != 3)
							|| (args.Length == 3 && args[2] != "true" && args[2] != "false"))
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_addWrongSyntax", session));
								return;
							}

							Areas.Add(new AlertArea(
								areaName,
								args.Length == 3 ? (args[2] == "true" ? true : false) : false
							));
							hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_areaAdded", session)
								.Replace("{name}", areaName));

							break;

						case "edit":

							if (areaIndex == -1)
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_areaNotFound", session)
									.Replace("{name}", areaName));
								return;
							}

							hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_editMode", session)
								.Replace("{name}", Areas[areaIndex].name));

							hurt.SendChatMessage(session, GetMsg("msg_helpEditCorner1", session));
							hurt.SendChatMessage(session, GetMsg("msg_helpEditCorner2", session));
							hurt.SendChatMessage(session, GetMsg("msg_helpEditName", session));
							hurt.SendChatMessage(session, GetMsg("msg_helpEditText", session));
							hurt.SendChatMessage(session, GetMsg("msg_helpEditConstant", session));

							editAreaName = areaName;

							break;

						case "remove":

							if (areaIndex == -1)
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_areaNotFound", session)
									.Replace("{name}", areaName));
								return;
							}

							hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_areaRemoved", session)
								.Replace("{name}", Areas[areaIndex].name));
							Areas.RemoveAt(areaIndex);

							break;
					}
					
					break;

					
				case "name":
				case "text":
				case "corner":
				case "constant":

					if(editAreaName == null)
					{
						hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_notEditMode", session));
						return;
					}

					areaIndex =	Areas.FindIndex(item => item.name.ToLower() == editAreaName.ToLower());

					if(areaIndex == -1)
					{
						hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_editAreaNotFound", session)
							.Replace("{name}", Areas[areaIndex].name));
						editAreaName = null;
						return;
					}


					switch(args[0])
					{
						case "name":
						
							if(args.Length != 2)
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_wrongSyntaxEditName", session));
								return;
							}
							
							string newName = args[1];
							int newNameIndex =	Areas.FindIndex(item => item.name.ToLower() == newName.ToLower());

							if(newNameIndex != -1)
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_areaAlreadyExists", session)
									.Replace("{name}", Areas[newNameIndex].name));
								return;
							}

							Areas[areaIndex].name = newName;

							hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_editNameSuccess", session)
								.Replace("{newName}", Areas[areaIndex].name));

							break;
							
							
						case "text":
						
							if(args.Length < 2)
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_wrongSyntaxEditText", session));
								return;
							}

							string[] textArr = new string[args.Length - 1];
							for (int i = 0, j = 0; i < textArr.Length; i++, j++)
							{
								if (i == 0) j++;
								textArr[i] = args[j];
							}
							string text = String.Join(" ", textArr);

							Areas[areaIndex].alertText = text;

							hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_editTextSuccess", session)
								.Replace("{name}", Areas[areaIndex].name));

							break;
							
							
						case "corner":

							if(args.Length != 2 || (args[1] != "1" && args[1] != "2"))
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_wrongSyntaxEditCorner", session));
								return;
							}

							switch(args[1])
							{
								case "1":
									Areas[areaIndex].firstCornerX = (float)Math.Round((decimal)session.WorldPlayerEntity.transform.position.x, 1);
									Areas[areaIndex].firstCornerZ = (float)Math.Round((decimal)session.WorldPlayerEntity.transform.position.z, 1);
									break;

								case "2":
									Areas[areaIndex].secondCornerX = (float)Math.Round((decimal)session.WorldPlayerEntity.transform.position.x, 1);
									Areas[areaIndex].secondCornerZ = (float)Math.Round((decimal)session.WorldPlayerEntity.transform.position.z, 1);
									break;
							}

							hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_editCornerSuccess", session)
								.Replace("{name}", Areas[areaIndex].name)
								.Replace("{n}", args[1]));

							break;


						case "constant":

							if(args.Length != 2 || (args[1] != "true" && args[1] != "false"))
							{
								hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_wrongSyntaxEditConstant", session));
								return;
							}

							Areas[areaIndex].constant = args[1] == "true" ? true : false;

							hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_editConstantSuccess", session)
								.Replace("{name}", Areas[areaIndex].name)
								.Replace("{constantOrNot}", Areas[areaIndex].constant ? GetMsg("msg_constant", session) : GetMsg("msg_notConstant", session)));

							break;
					}

					break;

				default:
					hurt.SendChatMessage(session, msgPrefix, GetMsg("msg_wrongSyntaxMain", session));
					return;
			}

			SaveAlertAreas();
		}


        string GetMsg(string key, object userID = null)
        {
            return (userID != null) ? lang.GetMessage(key, this, userID.ToString()) : lang.GetMessage(key, this);
        }
	}
}
