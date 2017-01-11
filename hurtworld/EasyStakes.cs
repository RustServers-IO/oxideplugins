﻿//Reference: UnityEngine.UI
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("EasyStakes", "Noviets", "1.0.9", ResourceId = 1697)]
    [Description("Fill up every authorized stake with amber.")]

    class EasyStakes : HurtworldPlugin
    {		

		void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"nopermission","EasyStake: You don't have Permission to do this!"},
				{"stakes","EasyStakes: You're Authorized on {Count} stakes. You need {Amount} Amber to fill them all."},
				{"stakesfilled","EasyStakes: Successfully filled {count} Ownership Stakes with {amount} Amber"},
				{"noamber","EasyStakes: You've ran out of Amber!"},
				{"share","EasyStakes: You've Authorized {Player} on {Amount} of your Ownership Stakes."},
				{"invalidplayer","EasyStakes: Player not found."},
				{"invaliddistance","EasyStakes: Distance must be a number."}
            };
			
			lang.RegisterMessages(messages, this);
        }
		void Loaded()
        {
            permission.RegisterPermission("easystakes.use", this);
			permission.RegisterPermission("easystakes.share", this);
			LoadDefaultMessages();
		}
		
		private PlayerSession GetSession(string name)
		{
			foreach(PlayerSession session in GameManager.Instance.GetSessions().Values)
			{
				if(session != null && session.IsLoaded)
					if(name == session.Name) return session;
			}
			foreach(PlayerSession session in GameManager.Instance.GetSessions().Values)
			{
				if(session != null && session.IsLoaded)
					if(session.Name.ToLower().Contains(name.ToLower())) return session;
			}
			return null;
		}

		string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);

		[ChatCommand("stakes")]
        void stakeCommand(PlayerSession session, string command, string[] args)
        {
			int auth=0;
			int AmberNeeded = 0;
			if(permission.UserHasPermission(session.SteamId.ToString(), "easystakes.use"))
			{
				if(args.Length == 0)
				{
					foreach (OwnershipStakeServer stake in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>())
					{
						if(stake.AuthorizedPlayers.Contains(session.Identity))
						{
							Inventory inv = stake.GetComponent<Inventory>() as Inventory;
							ItemInstance slot = inv.GetSlot(0);
							int Needs = 5 - slot.StackSize;
							AmberNeeded += Needs;
							auth++;
						}
					}
					hurt.SendChatMessage(session, Msg("stakes",session.SteamId.ToString()).Replace("{Count}",auth.ToString()).Replace("{Amount}",AmberNeeded.ToString()));
				}
				if(args.Length == 1)
				{
					if(args[0].ToLower() == "fill")
					{
						foreach (OwnershipStakeServer stake in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>())
						{
							if(stake.AuthorizedPlayers.Contains(session.Identity))
							{
								PlayerInventory pinv = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
								Inventory inv = stake.GetComponent<Inventory>() as Inventory;
								ItemInstance slot = inv.GetSlot(0);
								int Needs = 5 - slot.StackSize;
								AmberNeeded += Needs;
								auth++;
								if((bool)TakeAmber(pinv, Needs))
								{
									slot.StackSize = 5;
								}
								else
								{
									hurt.SendChatMessage(session, Msg("noamber",session.SteamId.ToString()));
									return;
								}
							}
						}
						hurt.SendChatMessage(session, Msg("stakesfilled",session.SteamId.ToString()).Replace("{count}",auth.ToString()).Replace("{amount}",AmberNeeded.ToString()));
					}
					if(args[0].ToLower() == "share")
					{
						if(permission.UserHasPermission(session.SteamId.ToString(), "easystakes.share"))
						{
							int Amount = 0;
							PlayerSession target = GetSession(args[1]);
							if(target != null)
							{
								if(args.Length == 2)
								{
									foreach (OwnershipStakeServer stake in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>())
									{
										if(stake.AuthorizedPlayers.Contains(session.Identity))
										{
											if(!stake.AuthorizedPlayers.Contains(target.Identity))
											{
												stake.AuthorizedPlayers.Add(target.Identity);
												Amount++;
											}
										}
									}
								}
								if(args.Length == 3)
								{
									int Distance = 0;
									try{Convert.ToInt32(args[2]);} catch{ hurt.SendChatMessage(session, Msg("invaliddistance",session.SteamId.ToString()));return;}
									foreach (OwnershipStakeServer stake in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>())
									{
										if(Vector3.Distance(stake.gameObject.transform.position, session.WorldPlayerEntity.transform.position) <= Distance)
										{
											if(stake.AuthorizedPlayers.Contains(session.Identity))
											{
												if(!stake.AuthorizedPlayers.Contains(target.Identity))
												{
													stake.AuthorizedPlayers.Add(target.Identity);
													Amount++;
												}
											}
										}
									}
								}
								hurt.SendChatMessage(session, Msg("share",session.SteamId.ToString()).Replace("{Player}",target.Name).Replace("{Amount}",Amount.ToString()));
							}
							else
								hurt.SendChatMessage(session, Msg("invalidplayer",session.SteamId.ToString()));
						}
						else 
							hurt.SendChatMessage(session, Msg("nopermission",session.SteamId.ToString()));
					}
				}
			}
			else 
				hurt.SendChatMessage(session, Msg("nopermission",session.SteamId.ToString()));
		}

		object TakeAmber(PlayerInventory pinv, int amount)
		{
			var slot = pinv.Items;
			var ItemMgr = Singleton<GlobalItemManager>.Instance;
			for (var i = 0; i < pinv.Items.Length; i++)
			{
				if (slot[i] != null)
				{
					if (slot[i].Item.ItemId == 87) 
					{
						if(slot[i].StackSize >= amount)
						{
							slot[i].StackSize = slot[i].StackSize - amount;
							pinv.Invalidate();
							return true;
						}
					}
				}
			}
			return false;
		}
	}
}