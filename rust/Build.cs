using Facepunch;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Build", "Reneb & NoGrod", "1.2.4", ResourceId = 715)]
	
    class Build : RustPlugin
    {
		private string permName = "build.builder";
	
        private enum SocketType
        {
            Wall,
            Floor,
            Bar,
            Block,
            FloorTriangle,
            Door
        }
		
        private class BuildPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public string currentPrefab;
            public string currentType;
            public float currentHealth;
            public Quaternion currentRotate;
            public BuildingGrade.Enum currentGrade;
            public bool ispressed;
            public float lastTickPress;
            public float currentHeightAdjustment;
            public string selection;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = true;
                ispressed = false;
            }

            void FixedUpdate() 
            {
                if(player.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY) && !ispressed)
                {
                    lastTickPress = Time.realtimeSinceStartup;
                    ispressed = true;
                    DoAction(this);
                }
                else if(player.serverInput.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    if((Time.realtimeSinceStartup - lastTickPress) > 1f)
                    {
                        DoAction(this);
                    }
                }
                else
                {
                    ispressed = false;
                }
            }

            public void LoadMsgGui(string Msg)
            {				
				DestroyGui();
                CuiHelper.AddUi(player, UI_BUILD.Replace("{HEADER}", Msg));
            }

            public void DestroyGui()
            {
                CuiHelper.DestroyUi(player, "UI_BUILD_HEADER");
				CuiHelper.DestroyUi(player, "UI_BUILD_MARKER");
            }
			
			public void OnDestroy () 
			{
				DestroyGui();
			}
        }

        private static Dictionary<string, string> deployedToItem;
        private Dictionary<string, string> nameToBlockPrefab;
        private static Dictionary<string, SocketType> nameToSockets;
        private static Dictionary<SocketType, object> TypeToType;
        private static List<string> resourcesList;
        private static Dictionary<string, string> animalList;
		private static int constructionColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Deployable", "Prevent Building", "Deployed" });

        private static Quaternion currentRot;
        private static Vector3 closestHitpoint;
        private static object closestEnt;
        private static Quaternion newRot;
        private string buildType;
        private string prefabName;
        private static int defaultGrade;
        private static float defaultHealth;
        private static Vector3 newPos;
        private static float distance;
        private static Dictionary<SocketType, object> sourceSockets;
        private static SocketType targetsocket;
        private static SocketType sourcesocket;
        private static Dictionary<Vector3, Quaternion> newsockets;
        private static float heightAdjustment;
        private static BasePlayer currentplayer;
        private static Collider currentCollider;
        private static BaseNetworkable currentBaseNet;
        private static List<object> houseList;
        private static List<Vector3> checkFrom;
        private static Item newItem;

        private static Quaternion defaultQuaternion = new Quaternion(0f, 0f, 0f, 1f);

		//Hooks
	
		private void Init()
		{
			permission.RegisterPermission(permName, this);

			Dictionary<string, Dictionary<string, string>> compiledLangs = new Dictionary<string, Dictionary<string, string>>();

			foreach(var line in messages)
			{
				foreach(var translate in line.Value)
				{
					if(!compiledLangs.ContainsKey(translate.Key))
						compiledLangs[translate.Key] = new Dictionary<string, string>();

					compiledLangs[translate.Key][line.Key] = translate.Value;
				}
			}

			foreach(var cLangs in compiledLangs)
			{
				lang.RegisterMessages(cLangs.Value, this, cLangs.Key);
			}
		}

        private void OnServerInitialized()
        {
            InitializeBlocks();
            InitializeSockets();
            InitializeDeployables();
            InitializeAnimals();
            InitializeResources();
        }

        private void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<BuildPlayer>();
            
			if(objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }
		
		//Other methods
		
        private void InitializeAnimals()
        {
            animalList = new Dictionary<string, string>();
            
			foreach (var str in GameManifest.Current.pooledStrings)
            {
                if(str.str.Contains("/rust.ai/agents/") && str.str.EndsWith(".prefab"))
                {
                    var animalPrefab = str.str.Substring(str.str.LastIndexOf('/') + 1);
                    animalList.Add(animalPrefab.Remove(animalPrefab.Length - 7), str.str);
                }
            }
        }

        private void InitializeDeployables()
        {
			deployedToItem = new Dictionary<string, string>();
			
            var allItemsDef = ItemManager.itemList;
            
			foreach (var itemDef in allItemsDef)
            {
                if(itemDef.GetComponent<ItemModDeployable>() != null)
                {
                    deployedToItem.Add(itemDef.displayName.english.ToLower(), itemDef.shortname);
                }
            }
        }
		
        private void InitializeResources()
        {
            resourcesList = new List<string>();
            
			var filesField = typeof(FileSystem_AssetBundles).GetField("files", BindingFlags.Instance | BindingFlags.NonPublic);
            var files = (Dictionary<string, AssetBundle>)filesField.GetValue(FileSystem.iface);
            
			foreach (var str in files.Keys)
            {
                if((str.StartsWith("assets/content/") || str.StartsWith("assets/bundled/") || str.StartsWith("assets/prefabs/")) && str.EndsWith(".prefab"))
                {
                    if(str.Contains(".worldmodel.")
					|| str.Contains("/fx/")
					|| str.Contains("/effects/")
					|| str.Contains("/build/skins/")
					|| str.Contains("/_unimplemented/")
					|| str.Contains("/ui/")
					|| str.Contains("/sound/")
					|| str.Contains("/world/")
					|| str.Contains("/env/")
					|| str.Contains("/clothing/")
					|| str.Contains("/skins/")
					|| str.Contains("/decor/")
					|| str.Contains("/monument/")
					|| str.Contains("/crystals/")
					|| str.Contains("/projectiles/")
					|| str.Contains("/meat_")
					|| str.EndsWith(".skin.prefab")
					|| str.EndsWith(".viewmodel.prefab")
					|| str.EndsWith("_test.prefab")
					|| str.EndsWith("_collision.prefab")
					|| str.EndsWith("_ragdoll.prefab")
					|| str.EndsWith("_skin.prefab")
					|| str.Contains("/clutter/"))
                        continue;
                    
					var gmobj = GameManager.server.FindPrefab(str);

                    if(gmobj?.GetComponent<BaseEntity>() != null)
                        resourcesList.Add(str);
                }
            }
        }

        private void InitializeSockets()
        {
            // PrefabName to SocketType
            nameToSockets = new Dictionary<string, SocketType>();

            // Get all possible sockets from the SocketType
            TypeToType = new Dictionary<SocketType, object>();


            // Sockets that can connect on a Floor / Foundation type
            var FloorType = new Dictionary<SocketType, object>();

            // Floor to Floor sockets
            var FloortoFloor = new Dictionary<Vector3, Quaternion>();
            FloortoFloor.Add(new Vector3(0f, 0f, -3f), new Quaternion(0f, 1f, 0f, 0f));

            //FloortoFloor.Add(new Vector3(-3f, 0f, 0f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            FloortoFloor.Add(new Vector3(-3f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));
            FloortoFloor.Add(new Vector3(0f, 0f, 3f), new Quaternion(0f, 0f, 0f, 1f));
            //FloortoFloor.Add(new Vector3(3f, 0f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            FloortoFloor.Add(new Vector3(3f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            // Floor to FloorTriangle sockets
            var FloortoFT = new Dictionary<Vector3, Quaternion>();
            FloortoFT.Add(new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 1f, 0f, 0f));
            FloortoFT.Add(new Vector3(-1.5f, 0f, 0f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            FloortoFT.Add(new Vector3(0f, 0f, 1.5f), new Quaternion(0f, 0f, 0f, 1f));
            FloortoFT.Add(new Vector3(1.5f, 0f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));

            // Floor to Wall sockets
            var FloortoWall = new Dictionary<Vector3, Quaternion>();
            FloortoWall.Add(new Vector3(0f, 0f, 1.5f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            FloortoWall.Add(new Vector3(-1.5f, 0f, 0f), new Quaternion(0f, 1f, 0f, 0f));
            FloortoWall.Add(new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            FloortoWall.Add(new Vector3(1.5f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            // Floor to Blocks sockets
            var FloorToBlock = new Dictionary<Vector3, Quaternion>();
            FloorToBlock.Add(new Vector3(0f, 0.1f, 0f), new Quaternion(0f, 1f, 0f, 0f));

            // Adding all informations from the Floor type into the main table
            FloorType.Add(SocketType.Block, FloorToBlock);
            FloorType.Add(SocketType.Wall, FloortoWall);
            FloorType.Add(SocketType.Floor, FloortoFloor);
            FloorType.Add(SocketType.FloorTriangle, FloortoFT);
            TypeToType.Add(SocketType.Floor, FloorType);

            // Sockets that can connect on a Wall type
            var WallType = new Dictionary<SocketType, object>();

            // Wall to Wall sockets
            var WallToWall = new Dictionary<Vector3, Quaternion>();
            WallToWall.Add(new Vector3(0f, 0f, -3f), new Quaternion(0f, 1f, 0f, 0f));
            WallToWall.Add(new Vector3(0f, 0f, 3f), new Quaternion(0f, 0f, 0f, 1f));

            // Wall to Wall Floor sockets
            var WallToFloor = new Dictionary<Vector3, Quaternion>();
            WallToFloor.Add(new Vector3(1.5f, 3f, 0f), new Quaternion(0f, 0.7071068f, 0f, -0.7071068f));
            WallToFloor.Add(new Vector3(-1.5f, 3f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));

            // Wall to Door sockets
            var WallToDoor = new Dictionary<Vector3, Quaternion>();
            WallToDoor.Add(new Vector3(0f, 0f, 0f), new Quaternion(0f, 1f, 0f, 0f));

            var WallToBar = new Dictionary<Vector3, Quaternion>();
            WallToBar.Add(new Vector3(0f, 1f, 0f), new Quaternion(0f, 1f, 0f, 0f));

            // Adding all informations from the Wall type into the main table
            // Note that you can't add blocks on a wall
            WallType.Add(SocketType.Floor, WallToFloor);
            WallType.Add(SocketType.Wall, WallToWall);
            WallType.Add(SocketType.Door, WallToDoor);
            WallType.Add(SocketType.Bar, WallToBar);
            TypeToType.Add(SocketType.Wall, WallType);

            // Sockets that can connect on a Block type
            var BlockType = new Dictionary<SocketType, object>();

            // Block to Block sockets
            var BlockToBlock = new Dictionary<Vector3, Quaternion>();
            BlockToBlock.Add(new Vector3(0f, 1.5f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            // For safety reasons i didn't put pillars or walls here
            // If needed it could easily be added
            BlockType.Add(SocketType.Block, BlockToBlock);
            TypeToType.Add(SocketType.Block, BlockType);

            // Sockets that can connect on a Floor/Foundation Triangles  type
            var FloorTriangleType = new Dictionary<SocketType, object>();

            // Floor Triangles to Floor Triangles type
            var FTtoFT = new Dictionary<Vector3, Quaternion>();
            FTtoFT.Add(new Vector3(0f, 0f, 0f), new Quaternion(0f, 1f, 0f, 0.0000001629207f));
            FTtoFT.Add(new Vector3(-0.75f, 0f, 1.299038f), new Quaternion(0f, 0.4999998f, 0f, -0.8660255f));
            FTtoFT.Add(new Vector3(0.75f, 0f, 1.299038f), new Quaternion(0f, 0.5000001f, 0f, 0.8660254f));
            FloorTriangleType.Add(SocketType.FloorTriangle, FTtoFT);

            // Floor Triangles to Wall type
            var FTtoWall = new Dictionary<Vector3, Quaternion>();
            FTtoWall.Add(new Vector3(0f, 0f, 0f), new Quaternion(0f, 0.7f, 0f, 0.7000001629207f));
            FTtoWall.Add(new Vector3(-0.75f, 0f, 1.299038f), new Quaternion(0f, 0.96593f, 0f, -0.25882f));
            FTtoWall.Add(new Vector3(0.75f, 0f, 1.299038f), new Quaternion(0f, -0.25882f, 0f, 0.96593f));
            FloorTriangleType.Add(SocketType.Wall, FTtoWall);

            // Floor Triangles to Floor type is a big fail, need to work on that still
            var FTtoFloor = new Dictionary<Vector3, Quaternion>();
            FTtoFloor.Add(new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 1f, 0f, 0f));
            FTtoFloor.Add(new Vector3(-2.0490381f, 0f, 2.0490381f), new Quaternion(0f, 0.5f, 0f, -0.8660254f));
            FTtoFloor.Add(new Vector3(2.0490381f, 0f, 2.0490381f), new Quaternion(0f, 0.5f, 0f, 0.8660254f));
            FloorTriangleType.Add(SocketType.Floor, FTtoFloor);

            // So at the moment only Floor and Foundation triangles can connect to easy other.
            TypeToType.Add(SocketType.FloorTriangle, FloorTriangleType);

            nameToSockets.Add("assets/prefabs/building core/foundation/foundation.prefab", SocketType.Floor);
            nameToSockets.Add("assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab", SocketType.FloorTriangle);
            nameToSockets.Add("assets/prefabs/building core/floor.triangle/floor.triangle.prefab", SocketType.FloorTriangle);
            nameToSockets.Add("assets/prefabs/building core/roof/roof.prefab", SocketType.Floor);
            nameToSockets.Add("assets/prefabs/building core/floor/floor.prefab", SocketType.Floor);
            nameToSockets.Add("assets/prefabs/building core/wall/wall.prefab", SocketType.Wall);
            nameToSockets.Add("assets/prefabs/building core/wall.doorway/wall.doorway.prefab", SocketType.Wall);
            nameToSockets.Add("assets/prefabs/building core/wall.window/wall.window.prefab", SocketType.Wall);
			nameToSockets.Add("assets/prefabs/building core/wall.frame/wall.frame.prefab", SocketType.Wall);
            nameToSockets.Add("assets/prefabs/building/wall.window.bars/wall.window.bars.wood.prefab", SocketType.Bar);
            nameToSockets.Add("assets/prefabs/building/wall.window.bars/wall.window.bars.metal.prefab", SocketType.Bar);
            nameToSockets.Add("assets/prefabs/building/wall.window.bars/wall.window.bars.toptier.prefab", SocketType.Bar);
			nameToSockets.Add("assets/prefabs/building core/wall.low/wall.low.prefab", SocketType.Wall);
			nameToSockets.Add("assets/prefabs/building core/wall.half/wall.half.prefab", SocketType.Wall);
            nameToSockets.Add("assets/prefabs/building core/stairs.l/block.stair.lshape.prefab", SocketType.Block);
            nameToSockets.Add("assets/prefabs/building core/stairs.u/block.stair.ushape.prefab", SocketType.Block);
            nameToSockets.Add("assets/prefabs/building/door.hinged/door.hinged.wood.prefab", SocketType.Door);
            nameToSockets.Add("assets/prefabs/building/door.hinged/door.hinged.metal.prefab", SocketType.Door);
            nameToSockets.Add("assets/prefabs/building/door.hinged/door.hinged.toptier.prefab", SocketType.Door);
            nameToSockets.Add("assets/prefabs/building core/foundation.steps/foundation.steps.prefab", SocketType.Floor);
        }

        private void InitializeBlocks()
        {
			nameToBlockPrefab = new Dictionary<string, string>();
			
            var constructions = PrefabAttribute.server.GetAll<Construction>();
            
			foreach (var construction in constructions)
            {
				//Puts(construction.info.name.english.ToString());
				
				/*if(construction.info.name.english.ToString().Contains("Triangle"))
				{
					Socket_Base[] socketArray = (Socket_Base[])socks.GetValue(construction);
					Debug.Log(socketArray.ToString());
					
					foreach (Socket_Base socket in socketArray)
					{
						//Puts(string.Format("{0} {1} {2} {3}", socket.name, socket.type.ToString(), socket.position.ToString(), socket.rotation.w.ToString()));
						Puts(string.Format("{0} - {1} {2} {3} {4} - {5} {6} {7}", socket.socketName, socket.rotation.x.ToString(), socket.rotation.y.ToString(), socket.rotation.z.ToString(), socket.rotation.w.ToString(), socket.position.x.ToString(), socket.position.y.ToString(), socket.position.z.ToString()));
					}
					
					Puts("================");
				}*/
				
                //Puts(construction.hierachyName + " " + construction.fullName);
                
				nameToBlockPrefab[construction.hierachyName] = construction.fullName;
            }
        }
		
		private bool HasAccess(BasePlayer player, string permName)
		{
			return player.IsAdmin || permission.UserHasPermission(player.UserIDString, permName);
		}
		
        private static bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = Quaternion.identity;

			if(player.serverInput.current == null)
                return false;

            viewAngle = Quaternion.Euler(player.serverInput.current.aimAngles);
            
			return true;
        }

        private static bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            Ray ray = new Ray(sourceEye, sourceDir * Vector3.forward);

            var hits = Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            
			foreach (var hit in hits)
            {
                if(hit.collider.GetComponentInParent<TriggerBase>() == null)
                {
                    if(hit.distance < closestdist)
                    {
                        closestdist = hit.distance;
                        closestEnt = hit.GetCollider();
                        closestHitpoint = hit.point;
                    }
                }
            }
			
            if(closestEnt is bool)
                return false;
			
            return true;
        }

        private static void SpawnDeployable(string prefab, Vector3 pos, Quaternion angles, BasePlayer player)
        {
            newItem = ItemManager.CreateByName(prefab);
            
			if(newItem?.info.GetComponent<ItemModDeployable>() == null)
                return;
            
			var deployable = newItem.info.GetComponent<ItemModDeployable>().entityPrefab.resourcePath;
            
			if(deployable == null)
                return;
            
			var newBaseEntity = GameManager.server.CreateEntity(deployable, pos, angles, true);
            
			if(newBaseEntity == null)
                return;
            
			newBaseEntity.SendMessage("SetDeployedBy", player, UnityEngine.SendMessageOptions.DontRequireReceiver);
            newBaseEntity.SendMessage("InitializeItem", newItem, UnityEngine.SendMessageOptions.DontRequireReceiver);
            
			newBaseEntity.Spawn();
        }

        private static void SpawnStructure(string prefabname, Vector3 pos, Quaternion angles, BuildingGrade.Enum grade, float health)
        {
            GameObject prefab = GameManager.server.CreatePrefab(prefabname, pos, angles, true);
            
			if(prefab == null)
                return;
            
			BuildingBlock block = prefab.GetComponent<BuildingBlock>();
            
			if(block == null) 
				return;
            
			block.transform.position = pos;
            block.transform.rotation = angles;
            block.gameObject.SetActive(true);
            block.blockDefinition = PrefabAttribute.server.Find<Construction>(block.prefabID);
            block.Spawn();
            block.SetGrade(grade);
			
            if(health <= 0f)
                block.health = block.MaxHealth();
            else
                block.health = health;
			
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        private static void SpawnResource(string prefab, Vector3 pos, Quaternion angles)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, angles, true);
            entity?.Spawn();
        }

        private static void SpawnAnimal(string prefab, Vector3 pos, Quaternion angles)
        {
            var createdPrefab = GameManager.server.CreateEntity(prefab, pos, angles, true);
            BaseEntity entity = createdPrefab?.GetComponent<BaseEntity>();
            entity?.Spawn();
        }

        private static bool isColliding(string name, Vector3 position, float radius)
        {
            var colliders = Physics.OverlapSphere(position, radius);
            
			foreach (var collider in colliders)
            {
                var block = collider.GetComponentInParent<BuildingBlock>();
                
				if(block != null && block.blockDefinition.fullName == name && Vector3.Distance(collider.transform.position, position) < 0.6f)
                    return true;
            }
			
            return false;
        }

        private static void SetGrade(BuildingBlock block, BuildingGrade.Enum level)
        {
            block.SetGrade(level);
            block.health = block.MaxHealth();
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        private static void SetHealth(BuildingBlock block)
        {
            block.health = block.MaxHealth();
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        private static void DoAction(BuildPlayer buildplayer)
        {
            currentplayer = buildplayer.player;
            
			if(!TryGetPlayerView(currentplayer, out currentRot))
                return;
            
			if(!TryGetClosestRayPoint(currentplayer.transform.position, currentRot, out closestEnt, out closestHitpoint))
                return;
            
			currentCollider = closestEnt as Collider;
            
			if(currentCollider == null)
                return;
            
			switch (buildplayer.currentType)
            {
                case "building":
                    DoBuild(buildplayer, currentplayer, currentCollider);
                    break;
                case "buildup":
                    DoBuildUp(buildplayer, currentplayer, currentCollider);
                    break;
                case "deploy":
                    DoDeploy(buildplayer, currentplayer, currentCollider);
                    break;
                case "plant":
                case "animal":
                    DoPlant(buildplayer, currentplayer, currentCollider);
                    break;
                case "grade":
                    DoGrade(buildplayer, currentplayer, currentCollider);
                    break;
                case "heal":
                    DoHeal(buildplayer, currentplayer, currentCollider);
                    break;
                case "erase":
                    DoErase(buildplayer, currentplayer, currentCollider);
                    break;
                case "rotate":
                    DoRotation(buildplayer, currentplayer, currentCollider);
                    break;
                case "spawning":
                    DoSpawn(buildplayer, currentplayer, currentCollider);
                    break;
                default:
                    return;
            }
        }

        private static void DoErase(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
			currentBaseNet = baseentity.GetComponentInParent<BaseNetworkable>();
            currentBaseNet?.Kill(BaseNetworkable.DestroyMode.Gib);
        }
		
        private static void DoPlant(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            newPos = closestHitpoint + (Vector3.up * buildplayer.currentHeightAdjustment);
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            SpawnAnimal(buildplayer.currentPrefab, newPos, newRot);
        }

        private static void DoDeploy(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            newPos = closestHitpoint + (Vector3.up * buildplayer.currentHeightAdjustment);
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            SpawnDeployable(buildplayer.currentPrefab, newPos, newRot, currentplayer);
        }

        private static void DoGrade(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            var fbuildingblock = baseentity.GetComponentInParent<BuildingBlock>();
           
			if(fbuildingblock == null)
                return;
            
			SetGrade(fbuildingblock, buildplayer.currentGrade);
            
			if(buildplayer.selection == "select")
                return;

            houseList = new List<object>();
            checkFrom = new List<Vector3>();
           
			houseList.Add(fbuildingblock);
            checkFrom.Add(fbuildingblock.transform.position);

            int current = 0;
			
            while(true)
            {
                if(++current > checkFrom.Count)
                    break;
				
                var hits = Physics.OverlapSphere(checkFrom[current - 1], 3.1f);
                
				foreach (var hit in hits)
                {
                    if(hit.GetComponentInParent<BuildingBlock>() != null)
                    {
                        fbuildingblock = hit.GetComponentInParent<BuildingBlock>();
                        
						if(!(houseList.Contains(fbuildingblock)))
                        {
                            houseList.Add(fbuildingblock);
                            checkFrom.Add(fbuildingblock.transform.position);
                            SetGrade(fbuildingblock, buildplayer.currentGrade);
                        }
                    }
                }
            }
        }

        private static void DoRotation(BuildingBlock block, Quaternion defaultRotation)
        {
            if(block.blockDefinition == null) 
				return;
            
			var transform = block.transform;
            
			if(defaultRotation == defaultQuaternion)
                transform.localRotation *= Quaternion.Euler(block.blockDefinition.rotationAmount);
            else
                transform.localRotation *= defaultRotation;

			block.RefreshEntityLinks();
			block.UpdateSurroundingEntities();
			
			/*if (block.currentSkin)
			{
				block.currentSkin.Refresh(block);
			}*/
			
			block.SendNetworkUpdateImmediate(false);
			block.ClientRPC(null, "RefreshSkin");			
        }

        private static void DoRotation(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            var buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
           
			if(buildingblock == null)
                return;
            
			DoRotation(buildingblock, buildplayer.currentRotate);
            
			if(buildplayer.selection == "select")
                return;

            houseList = new List<object>();
            checkFrom = new List<Vector3>();
            houseList.Add(buildingblock);
            checkFrom.Add(buildingblock.transform.position);

            int current = 0;
            
			while(true)
            {
                current++;
                
				if(current > checkFrom.Count)
                    break;
                
				var hits = Physics.OverlapSphere(checkFrom[current - 1], 3.1f);
                
				foreach (var hit in hits)
                {
                    var fbuildingblock = hit.GetComponentInParent<BuildingBlock>();
                    
					if(fbuildingblock != null && !(houseList.Contains(fbuildingblock)))
                    {
                        houseList.Add(fbuildingblock);
                        checkFrom.Add(fbuildingblock.transform.position);
                        DoRotation(fbuildingblock, buildplayer.currentRotate);
                    }
                }
            }
        }

        private static void DoHeal(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            var buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            
			if(buildingblock == null)
                return;
            
			SetHealth(buildingblock);
            
			if(buildplayer.selection == "select")
                return;
				
            houseList = new List<object>();
            checkFrom = new List<Vector3>();
            
			houseList.Add(buildingblock);
            checkFrom.Add(buildingblock.transform.position);
			
			int current = 0;
            
			while(true)
            {
                current++;
                
				if(current > checkFrom.Count)
                    break;
                
				List<BaseEntity> list = Pool.GetList<BaseEntity>();
                
				Vis.Entities<BaseEntity>(checkFrom[current - 1], 3f, list, constructionColl);
                
				for(int i = 0; i < list.Count; i++)
                {
                    BaseEntity hit = list[i];
                    var fbuildingblock = hit.GetComponentInParent<BuildingBlock>();
                    
					if(fbuildingblock != null && !(houseList.Contains(fbuildingblock)))
                    {
                        houseList.Add(fbuildingblock);
                        checkFrom.Add(fbuildingblock.transform.position);
                        SetHealth(fbuildingblock);
                    }
                }
            }
        }

        private static void DoSpawn(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            newPos = closestHitpoint + (Vector3.up * buildplayer.currentHeightAdjustment);
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            SpawnStructure(buildplayer.currentPrefab, newPos, newRot, buildplayer.currentGrade, buildplayer.currentHealth);
        }

        private static void DoBuildUp(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            var fbuildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            
			if(fbuildingblock == null)
                return;

            newPos = fbuildingblock.transform.position + (Vector3.up * buildplayer.currentHeightAdjustment);
            newRot = fbuildingblock.transform.rotation;
           
			if(isColliding(buildplayer.currentPrefab, newPos, 1f))
                return;
			
            SpawnStructure(buildplayer.currentPrefab, newPos, newRot, buildplayer.currentGrade, buildplayer.currentHealth);
        }

        private static void DoBuild(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            var fbuildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            
			if(fbuildingblock == null)
            {
                return;
            }
			
            distance = 999999f;
            Vector3 newPos = new Vector3(0f, 0f, 0f);
            newRot = new Quaternion(0f, 0f, 0f, 0f);
            
			//  Checks if this building has a socket hooked to it self
            //  If not ... well it won't be able to be built via AI
            if(nameToSockets.ContainsKey(fbuildingblock.blockDefinition.fullName))
            {
                sourcesocket = nameToSockets[fbuildingblock.blockDefinition.fullName];
                // Gets all Sockets that can be connected to the source building
                if(TypeToType.ContainsKey(sourcesocket))
                {
                    sourceSockets = (Dictionary<SocketType, object>)TypeToType[sourcesocket];
                    targetsocket = nameToSockets[buildplayer.currentPrefab];
                    // Checks if the newly built structure can be connected to the source building
                    if(sourceSockets.ContainsKey(targetsocket))
                    {
                        newsockets = (Dictionary<Vector3, Quaternion>)sourceSockets[targetsocket];
                        // Get all the sockets that can be hooked to the source building via the new structure element
                        foreach (KeyValuePair<Vector3, Quaternion> pair in newsockets)
                        {
                            var currentrelativepos = (fbuildingblock.transform.rotation * pair.Key) + fbuildingblock.transform.position;
                            if(Vector3.Distance(currentrelativepos, closestHitpoint) < distance)
                            {
                                // Get the socket that is the closest to where the player is aiming at
                                distance = Vector3.Distance(currentrelativepos, closestHitpoint);
                                newPos = currentrelativepos + (Vector3.up * buildplayer.currentHeightAdjustment);
                                newRot = (fbuildingblock.transform.rotation * pair.Value);
                            }
                        }
                    }
                }
            }
			
            if(newPos.x == 0f)
                return;
			
            // Checks if the element has already been built to prevent multiple structure elements on one spot
            if(isColliding(buildplayer.currentPrefab, newPos, 1f))
                return;

            SpawnStructure(buildplayer.currentPrefab, newPos, newRot, buildplayer.currentGrade, buildplayer.currentHealth);
        }

        private bool TryGetBuildingPlans(string arg, out string buildType, out string prefabName)
        {
            prefabName = "";
            buildType = "";
            
			int intbuilding;
            
			if(nameToBlockPrefab.ContainsKey(arg))
            {
                prefabName = nameToBlockPrefab[arg];
                buildType = "building";
                
				return true;
            }
            else if(deployedToItem.ContainsKey(arg.ToLower()))
            {
                prefabName = deployedToItem[arg.ToLower()];
                buildType = "deploy";
                
				return true;
            }
            else if(deployedToItem.ContainsValue(arg.ToLower()))
            {
                prefabName = arg.ToLower();
                buildType = "deploy";
                
				return true;
            }
            else if(animalList.ContainsKey(arg.ToLower()))
            {
                prefabName = animalList[arg.ToLower()];
                buildType = "animal";
                
				return true;
            }
            else if(int.TryParse(arg, out intbuilding))
            {
                if(intbuilding <= resourcesList.Count)
                {
                    prefabName = resourcesList[intbuilding];
                    buildType = "plant";
                    
					return true;
                }
            }
			
            return false;
        }

        private bool hasNoArguments(BasePlayer player, string[] args)
        {
            if(args.Length == 0)
            {
                var buildPlayer = player.GetComponent<BuildPlayer>();
				
                if(buildPlayer != null)
                {
                    buildPlayer.DestroyGui();
                    
					UnityEngine.Object.Destroy(buildPlayer);
                    
					SendReply(player, "Build Tool Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp");
				
                return true;
            }
			
            return false;
        }

        private BuildPlayer GetBuildPlayer(BasePlayer player)
        {
            return player.GetComponent<BuildPlayer>() ?? player.gameObject.AddComponent<BuildPlayer>();
        }

		//Chat commands
		
        [ChatCommand("build")]
        private void cmdChatBuild(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            if(hasNoArguments(player, args)) 
				return;

            if(!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
			
            if(buildType != "building")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
			
            BuildPlayer buildplayer = GetBuildPlayer(player);

            defaultGrade = 0;
            defaultHealth = -1f;
            heightAdjustment = 0f;

            if(args.Length > 1) 
				float.TryParse(args[1], out heightAdjustment);
			
            if(args.Length > 2) 
				int.TryParse(args[2], out defaultGrade);
			
            if(args.Length > 3) 
				float.TryParse(args[3], out defaultHealth);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHealth = defaultHealth;
            buildplayer.currentGrade = (BuildingGrade.Enum)defaultGrade;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            var msg = string.Format("Building Tool AIBuild: {0} - HeightAdjustment: {1} - Grade: {2} - Health: {3}", args[0], heightAdjustment.ToString(), buildplayer.currentGrade.ToString(), buildplayer.currentHealth.ToString());
            
			buildplayer.LoadMsgGui(msg);
           
			SendReply(player, msg);
        }

        [ChatCommand("spawn")]
        private void cmdChatSpawn(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            if(hasNoArguments(player, args)) 
				return;

            if(!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
			
            if(buildType == "building") 
				buildType = "spawning";
            else
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            BuildPlayer buildplayer = GetBuildPlayer(player);

            defaultGrade = 0;
            defaultHealth = -1f;
            heightAdjustment = 0f;

            if(args.Length > 1) 
				float.TryParse(args[1], out heightAdjustment);
			
            if(args.Length > 2) 
				int.TryParse(args[2], out defaultGrade);
			
            if(args.Length > 3) 
				float.TryParse(args[3], out defaultHealth);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHealth = defaultHealth;
            buildplayer.currentGrade = (BuildingGrade.Enum)defaultGrade;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            var msg = string.Format("Building Tool RawSpawning: {0} - HeightAdjustment: {1} - Grade: {2} - Health: {3}", args[0], heightAdjustment.ToString(), buildplayer.currentGrade.ToString(), buildplayer.currentHealth.ToString());
            buildplayer.LoadMsgGui(msg);
            SendReply(player, msg);
        }

        [ChatCommand("deploy")]
        private void cmdChatDeploy(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            if(hasNoArguments(player, args)) 
				return;

            if(!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp deployables");
                return;
            }
            if(buildType != "deploy")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp deployables");
                return;
            }
			
            BuildPlayer buildplayer = GetBuildPlayer(player);

            heightAdjustment = 0f;
			
            if(args.Length > 1) 
				float.TryParse(args[1], out heightAdjustment);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            var msg = string.Format("Building Tool Deploying: {0} - Height Adjustment: {1}", buildplayer.currentPrefab, buildplayer.currentHeightAdjustment.ToString());
           
			buildplayer.LoadMsgGui(msg);
           
			SendReply(player, msg);
        }
		
        [ChatCommand("erase")]
        void cmdChatErase(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            BuildPlayer buildplayer = GetBuildPlayer(player);
			
            if (buildplayer.currentType != null && buildplayer.currentType == "erase")
            {
                buildplayer.DestroyGui();
                
				UnityEngine.Object.Destroy(buildplayer);
                
				SendReply(player, "Building Tool: Remove Deactivated");
            } else {
                buildplayer.currentType = "erase";
                
				SendReply(player, "Building Tool: Remove Activated");

                var msg = "Building Tool: Remove Activated";
                
				buildplayer.LoadMsgGui(msg);
                
				SendReply(player, msg);
            }
        }
		
        [ChatCommand("plant")]
        private void cmdChatPlant(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            if(hasNoArguments(player, args)) 
				return;

            if(!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp resources");
                return;
            }
			
            if(buildType != "plant")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp resources");
                return;
            }
			
            BuildPlayer buildplayer = GetBuildPlayer(player);

            heightAdjustment = 0f;
			
            if(args.Length > 1) 
				float.TryParse(args[1], out heightAdjustment);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            var msg = string.Format("Building Tool Planting: {0} - HeightAdjustment: {1}", prefabName, buildplayer.currentHeightAdjustment.ToString());
            
			buildplayer.LoadMsgGui(msg);
            
			SendReply(player, msg);
        }

        [ChatCommand("animal")]
        private void cmdChatAnimal(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
            
			if(hasNoArguments(player, args)) 
				return;

            if(!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp animals");
                return;
            }
            
			if(buildType != "animal")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp animals");
                return;
            }
            
			BuildPlayer buildplayer = GetBuildPlayer(player);

            heightAdjustment = 0f;
            
			if(args.Length > 1) 
				float.TryParse(args[1], out heightAdjustment);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            var msg = string.Format("Building Tool Spawning Animals: {0} - HeightAdjustment: {1}", prefabName, heightAdjustment.ToString());
            
			buildplayer.LoadMsgGui(msg);
            
			SendReply(player, msg);
        }

        [ChatCommand("buildup")]
        private void cmdChatBuildup(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            if(hasNoArguments(player, args)) 
				return;

            if(!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
			
            if(buildType != "building")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
			
            BuildPlayer buildplayer = GetBuildPlayer(player);

            buildType = "buildup";
            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            defaultGrade = 0;
            defaultHealth = -1f;
            heightAdjustment = 3f;

            if(args.Length > 1) 
				float.TryParse(args[1], out heightAdjustment);
			
            if(args.Length > 2) 
				int.TryParse(args[2], out defaultGrade);
			
            if(args.Length > 3) 
				float.TryParse(args[3], out defaultHealth);

            buildplayer.currentHealth = defaultHealth;
            buildplayer.currentGrade = (BuildingGrade.Enum)defaultGrade;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            var msg = string.Format("Building Tool BuildUP: {0} - Height: {1} - Grade: {2} - Health: {3}", args[0], buildplayer.currentHeightAdjustment.ToString(), buildplayer.currentGrade.ToString(), buildplayer.currentHealth.ToString());
            
			buildplayer.LoadMsgGui(msg);
            
			SendReply(player, msg);
        }

        [ChatCommand("buildgrade")]
        private void cmdChatBuilGrade(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            if(hasNoArguments(player, args)) 
				return;
			
            BuildPlayer buildplayer = GetBuildPlayer(player);

			buildplayer.selection = "select";
            buildplayer.currentType = "grade";
 			
			defaultGrade = 0;           
			
			int.TryParse(args[0], out defaultGrade);
			
            if(args.Length > 1 && args[1] == "all")
                    buildplayer.selection = "all";
				
            buildplayer.currentGrade = (BuildingGrade.Enum)defaultGrade;

            var msg = string.Format("Building Tool SetGrade: {0} - for {1}", buildplayer.currentGrade.ToString(), buildplayer.selection);
            
			buildplayer.LoadMsgGui(msg);
            
			SendReply(player, msg);
        }

        [ChatCommand("buildheal")]
        private void cmdChatBuilHeal(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            if(hasNoArguments(player, args)) 
				return;
			
            BuildPlayer buildplayer = GetBuildPlayer(player);

            buildplayer.currentType = "heal";
            buildplayer.selection = "select";
			
            if(args.Length > 0 && args[0] == "all")
				buildplayer.selection = "all";

            var msg = string.Format("Building Tool Heal for: {0}", buildplayer.selection);
            
			buildplayer.LoadMsgGui(msg);
            
			SendReply(player, msg);
        }

        [ChatCommand("buildrotate")]
        private void cmdChatBuilRotate(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            BuildPlayer buildplayer = GetBuildPlayer(player);

            buildplayer.currentType = "rotate";
            buildplayer.selection = "select";
            
			float rotate = 0f;

            if(args.Length > 0) 
				float.TryParse(args[0], out rotate);
            
			if(args.Length > 1 && args[1] == "all")
				buildplayer.selection = "all";
			
            buildplayer.currentRotate = Quaternion.Euler(0f, rotate, 0f);

            var msg = string.Format("Building Tool Rotation for: {0}", buildplayer.selection);
            
			buildplayer.LoadMsgGui(msg);
            
			SendReply(player, msg);
        }

        [ChatCommand("buildhelp")]
        private void cmdChatBuildhelp(BasePlayer player, string command, string[] args)
        {
            if(!HasAccess(player, permName)) 
				return;
			
            if(args.Length == 0)
            {
                SendReply(player, "======== Buildings ========");
                SendReply(player, "/buildhelp buildings");
                SendReply(player, "/buildhelp grades");
                SendReply(player, "/buildhelp heal");
                SendReply(player, "======== Deployables ========");
                SendReply(player, "/buildhelp deployables");
                SendReply(player, "======== Resources (Trees, Ores, Barrels) ========");
                SendReply(player, "/buildhelp resources");
                SendReply(player, "======== Animals ========");
                SendReply(player, "/buildhelp animals");
                SendReply(player, "======== Erase ========");
                SendReply(player, "/buildhelp erase");
				
				return;
            }
			
            if(args[0].ToLower() == "buildings")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/build StructureName Optional:HeightAdjust(can be negative, 0 default) Optional:Grade Optional:Health");
                SendReply(player, "/buildup StructureName Optional:HeightAdjust(can be negative, 3 default) Optional:Grave Optional:Health");
                SendReply(player, "/buildrotate");
                SendReply(player, "======== Usage ========");
                SendReply(player, "/build foundation => build a Twigs Foundation");
                SendReply(player, "/build foundation 0 2 => build a Stone Foundation");
                SendReply(player, "/build wall 0 3 1 => build a Metal Wall with 1 health");
                SendReply(player, "======== List ========");
                SendReply(player, "/build foundation - /build foundation.triangle - /build foundation.steps(not avaible)");
                SendReply(player, "/build block.halfheight - /build block.halfheight.slanted (stairs)");
                SendReply(player, "/build wall - /build wall.low - /build wall.doorway - /build wall.window - /build wall.half - /build wall.frame");
                SendReply(player, "/build floor - /build floor.triangle - /build roof");
            }
            else if(args[0].ToLower() == "grades")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/buildgrade GradeLevel Optional:all => default is only the selected block");
                SendReply(player, "======== Usage ========");
                SendReply(player, "/buildgrade 0 => set grade 0 for the select block");
                SendReply(player, "/buildgrade 2 all => set grade 2 (Stone) for the entire building");
            }
            else if(args[0].ToLower() == "heal")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/buildheal Optional:all => default is only the selected block");
                SendReply(player, "======== Usage ========");
                SendReply(player, "/buildheal all => will heal your entire structure");
            }
            else if(args[0].ToLower() == "deployables")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/deploy \"Deployable Name\" Optional:HeightAdjust(can be negative, 0 default)");
                SendReply(player, "======== Usage ========");
                SendReply(player, "/deploy \"Tool Cupboard\" => build a Tool Cupboard");
            }
            else if(args[0].ToLower() == "resources")
            {
                int i = 0;
                SendReply(player, "======== Commands ========");
                SendReply(player, "/plant \"Resource ID\"");
                SendReply(player, "Please check in your console to see the full list");
                PrintToConsole(player, "======== Plant List ========");
                
				foreach (string resource in resourcesList)
                {
                    PrintToConsole(player, string.Format("{0} - {1}", i, resource));
                    i++;
                }
            }
            else if(args[0].ToLower() == "animals")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/animal \"Name\"");
                SendReply(player, "======== List ========");
                
				foreach (KeyValuePair<string, string> pair in animalList)
                {
                    SendReply(player, string.Format("{0}", pair.Key));
                }
            }
        }

		//UI
		
        public static string UI_BUILD = @"
		[{
			""name"": ""UI_BUILD_HEADER"",
			""parent"": ""Hud"",
			""components"":
			[
				{
					 ""type"":""UnityEngine.UI.Image"",
					 ""color"":""0.15 0.15 0.15 0.7"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.25 0.945"",
					""anchormax"": ""0.75 0.987""
				}
			]
		},
		{
			""parent"": ""UI_BUILD_HEADER"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""{HEADER}"",
					""fontSize"":15,
					""align"": ""MiddleCenter"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0 0.1"",
					""anchormax"": ""1 0.8""
				}
			]
		},
		{
			""name"": ""UI_BUILD_MARKER"",
			""parent"": ""Hud"",
			""fadeOut"": ""0.4"",
			""components"":
			[
				{
					""type"": ""UnityEngine.UI.Text"",
					""text"": ""•"",
					""fontSize"": 20,
					""align"": ""MiddleCenter"",
				},
				{
					""type"": ""RectTransform"",
					""anchormin"": ""0.48 0.48"", 
					""anchormax"": ""0.52 0.52""
				}
			]
		}]";
		
		//Languages phrases

		private readonly Dictionary<string, Dictionary<string, string>> messages = new Dictionary<string, Dictionary<string, string>>
		{
		};		
    }
}
