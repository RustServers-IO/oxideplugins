using System.Collections.Generic;
using Oxide.Game.Rust;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
	[Info( "AdminHammer", "mvrb", "1.0.0" )]
	class AdminHammer : RustPlugin
	{
		const string permAllow = "adminhammer.allow";		
		bool logToConsole = true;
		float toolDistance = 200f;
		string toolUsed = "hammer";
		bool showSphere = false;
		
		int layerMask = LayerMask.GetMask( "Construction", "Deployed", "Default" );
		
		List<BasePlayer> Users = new List<BasePlayer>();
		
		protected override void LoadDefaultConfig()
        {
            Config["LogToConsole"] = logToConsole = GetConfig( "LogToFile", true );
            Config["ShowSphere"] = showSphere = GetConfig( "ShowSphere", false );
            Config["ToolDistance"] = toolDistance = GetConfig( "ToolDistance", 200f );
            Config["ToolUsed"] = toolUsed = GetConfig( "ToolUsed", "hammer" );

            SaveConfig();
        }
        
        void Init()
		{
			LoadDefaultConfig();
			LoadDefaultMessages();
			RegisterPermissions();
		}
		
		void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages( new Dictionary<string, string>
            {
                ["NoAuthorizedPlayers"] = "No authorized players.",
                ["AuthorizedPlayers"] = "Authorized players in the {0} owned by {1}:",
                ["NoEntityFound"] = "No entity found. Look at an entity and right-click while holding a {0}.",
                ["NoOwner"] = "No owner found for this entity.",
                ["ChatEntityOwnedBy"] = "This {0} is owned by {1}",
                ["ConsoleEntityOwnedBy"] = "This {0} is owned by www.steamcommunity.com/profiles/{1}",
                ["ToolActivated"] = "You have enabled AdminHammer.",
                ["ToolDeactivated"] = "You have disabled AdminHammer."
            }, this );
        }
		
		void RegisterPermissions() => permission.RegisterPermission( permAllow, this );
		
		[ChatCommand( "adminhammer" )]
		void cmdAdminHammer( BasePlayer player )
		{
			if ( !permission.UserHasPermission( player.UserIDString, permAllow ) ) return;
			
			if ( Users.Contains( player ) )
			{
				Users.Remove( player );
				player.ChatMessage( Lang( "ToolDeactivated", player.UserIDString ) );
			}
			else
			{
				Users.Add( player );
				player.ChatMessage( Lang( "ToolActivated", player.UserIDString ) );
			}
		}
		
		void OnPlayerInput( BasePlayer player, InputState input )
		{
			if ( !Users.Contains( player ) ) return;
			
			if ( permission.UserHasPermission( player.UserIDString, permAllow ) )
			{
				if ( !input.WasJustPressed( BUTTON.FIRE_SECONDARY ) || ( player.GetActiveItem() as Item )?.info.shortname != toolUsed ) return;
				
				RaycastHit hit;
				var raycast = Physics.Raycast( player.eyes.HeadRay(), out hit, toolDistance, layerMask );
				BaseEntity entity = raycast ? hit.GetEntity() : null;
				
				if ( !entity )
				{
					SendReply( player, Lang( "NoEntityFound", player.UserIDString, toolUsed ) );
					return;
				}
				
				if ( entity is BuildingPrivlidge || entity is AutoTurret )
				{
					player.ChatMessage( GetAuthorized( entity, player ) );
				}				
				else
				{
					player.ChatMessage( entity.OwnerID == 0 ? Lang( "NoOwner", player.UserIDString ) : Lang( "ChatEntityOwnedBy", player.UserIDString, entity.ShortPrefabName, GetName( entity.OwnerID.ToString() ) ) );
					Puts( entity.OwnerID == 0 ? Lang( "NoOwner", player.UserIDString ) : Lang( "ConsoleEntityOwnedBy", player.UserIDString, entity.ShortPrefabName, entity.OwnerID.ToString() ) );
				}
				
				if ( showSphere )
					player.SendConsoleCommand( "ddraw.sphere", 2f, Color.blue, entity.CenterPoint(), 1f );
			}
		}
		
		string GetAuthorized( BaseEntity entity, BasePlayer player )
		{
			string msg = Lang( "AuthorizedPlayers", player.UserIDString, entity.ShortPrefabName, GetName( entity.OwnerID.ToString() ) ) + "\n";			
			var turret = entity as AutoTurret;
			var priv = entity as BuildingPrivlidge;			
			int authed = 0;
			
			foreach ( var user in ( turret ? turret.authorizedPlayers : priv.authorizedPlayers ) )
			{
				msg += $"- {GetName( user.userid.ToString() )}\n";
				authed++;
			}
			
			return authed == 0 ? Lang( "NoAuthorizedPlayers", player.UserIDString ) : msg;
		}
				
		string GetName( string id ) { return RustCore.FindPlayer( id ) ? RustCore.FindPlayer( id ).displayName : covalence.Players.FindPlayer( id )?.Name; }
		
		T GetConfig<T>( string name, T value ) => Config[name] == null ? value : ( T )Convert.ChangeType( Config[name], typeof( T ) );
		
		string Lang( string key, string id = null, params object[] args ) => string.Format( lang.GetMessage( key, this, id ), args );
	}
}