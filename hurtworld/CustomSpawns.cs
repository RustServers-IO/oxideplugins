using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("CustomSpawns", "Trentu", "1.1.0")]
    [Description("Set some custom spawns.")]

    class CustomSpawns : HurtworldPlugin
    {
		class Location
        {
            public float x;
            public float y;
            public float z;

            public Location(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }
		
		//All Spawnpoint are loaded in here
		List<Location> SpawnPoints = new List<Location>();
		
		//Setup the Oxide Permissions!
		void LoadPermissions(){
			if (!permission.PermissionExists("customspawns.edit")) permission.RegisterPermission("customspawns.edit", this);
			if (!permission.PermissionExists("customspawns.list")) permission.RegisterPermission("customspawns.list", this);
			if (!permission.PermissionExists("customspawns.pos")) permission.RegisterPermission("customspawns.pos", this);
			if (!permission.PermissionExists("customspawns.tp")) permission.RegisterPermission("customspawns.tp", this);
			LoadData();
		}
		
		//Save Spawns in File
		void SaveSpawns()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Spawns", SpawnPoints);
        }

		//Load Spawndata
        void LoadData()
        {
			foreach (Location cords in Interface.Oxide.DataFileSystem.ReadObject<List<Location>>("Spawns"))
            {
				SpawnPoints.Add(cords);
			}
        }
		
        void Loaded() => LoadPermissions();

		//Player want a spawnpoint lets find one :D
        Vector3 OnFindSpawnPoint()
        {
			
			if(SpawnPoints.Count < 1){
				//Back to Default spawn!
				return new Vector3(-3708,188,-2392);
			}else{
				
				//Find a random spawn by using Unitys Random
				int SpawnID = UnityEngine.Random.Range(0, SpawnPoints.Count);
				return new Vector3(SpawnPoints[SpawnID].x,SpawnPoints[SpawnID].y,SpawnPoints[SpawnID].z);
				
			}
			
        }
		
		//Command for edit spawns
		[ChatCommand("spawn")]
        void SpawnCommand(PlayerSession session, string command, string[] args)
        {
			if(args.Length == 0){
				hurt.SendChatMessage(session, "Customspawns: -------Help------");	
				hurt.SendChatMessage(session, "Customspawns: /spawn set  = Sets an spawnpoint at your position");	
				hurt.SendChatMessage(session, "Customspawns: /spawn list = Draw a list of spawnpoints");	
				hurt.SendChatMessage(session, "Customspawns: /spawn pos  = Shows your position");	
				hurt.SendChatMessage(session, "Customspawns: /spawn delete <id> = Deletes an spawn point");	
				hurt.SendChatMessage(session, "Customspawns: /spawn tp <id> = Deletes an spawn point");	
			}else if(args[0] == "set" && permission.UserHasPermission(session.SteamId.ToString(), "customspawns.edit")){				
				//Add an spawnpoint
				Vector3 Position = session.WorldPlayerEntity.transform.position;
				SpawnPoints.Add(new Location(Position.x,Position.y,Position.z));
				hurt.SendChatMessage(session, "Customspawns: Created spawn: X:" + Position.x + " | Y:" + Position.y + " | Z:" + Position.z);	
				SaveSpawns();
				
			}else if(args[0] == "list" && permission.UserHasPermission(session.SteamId.ToString(), "customspawns.list")){
				//List all spawns
				string Message = "";
				for (int i = 0; i < SpawnPoints.Count; i++){
					Message = Message + "| ID:" + i + " | Cords: X:" + SpawnPoints[i].x + " | Y:" + SpawnPoints[i].y + " | Z:" + SpawnPoints[i].z;
				}
				
				hurt.SendChatMessage(session, "Customspawns: -------" + SpawnPoints.Count + " Spawns------");
				hurt.SendChatMessage(session, Message);
				
			}else if(args[0] == "delete" && permission.UserHasPermission(session.SteamId.ToString(), "customspawns.edit")){
				//Delete Spawnpoint
				//can the spawn exist?
				int ID;
				
				if(Int32.TryParse(args[1],out ID)){
				
					if(ID >= 0 && ID < SpawnPoints.Count){
						SpawnPoints.RemoveAt(ID);
						hurt.SendChatMessage(session, "Customspawns: Deleted " + ID);
						SaveSpawns();
					}else{
						hurt.SendChatMessage(session, "Customspawns: Invalid ID");
					}
					
				}else{
					hurt.SendChatMessage(session, "Customspawns: Invalid ID");
				}
				
			}else if(args[0] == "pos" && permission.UserHasPermission(session.SteamId.ToString(), "customspawns.pos")){
				//Show Current Position
				Vector3 PlayerPosition = session.WorldPlayerEntity.transform.position;
				hurt.SendChatMessage(session, "Customspawns: X:" + PlayerPosition.x + " | Y:" + PlayerPosition.y + " | Z:" + PlayerPosition.z);	
			}else if(args[0] == "tp" && permission.UserHasPermission(session.SteamId.ToString(), "customspawns.tp")){
				//Show Current Position		
				int ID;
				if(Int32.TryParse(args[1],out ID)){
					if(ID >= 0 && ID < SpawnPoints.Count){
						hurt.SendChatMessage(session, "Customspawns: Teleporting to " + ID);
						session.WorldPlayerEntity.transform.position = new Vector3(SpawnPoints[ID].x,SpawnPoints[ID].y,SpawnPoints[ID].z);
					}else{
						hurt.SendChatMessage(session, "Customspawns: Invalid ID");
					}	
				}else{
					hurt.SendChatMessage(session, "Customspawns: Invalid ID");
				}
			}
        }
    }
}
