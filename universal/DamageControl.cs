using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;

namespace Oxide.Plugins {
 [Info("DamageControl", "ColonBlow, MSpeedie", "2.2.5", ResourceId = 2677)]
 [Description("Allows authorized Users to control damage settings for time, animals, apc, buildings, bgrades, heli, npcs, players and zombies")]
 // internal class DamageControl : RustPlugin
 class DamageControl: CovalencePlugin {
  // note we check isAdmin as well so Admins get this by default
  readonly string permAdmin = "damagecontrol.admin";

  // toggles to make these immuned to damage if true
  public bool ProtectDeployed;
  public bool ProtectDoor;
  public bool ProtectFloor;
  public bool ProtectFoundation;
  public bool ProtectHighExternal;
  public bool ProtectOther;
  public bool ProtectRoof;
  public bool ProtectStairs;
  public bool ProtectTC;
  public bool ProtectWall;

  // these are to make checking look cleaner
  // action
  readonly List < string > dcaction = new List < string > {
   "help",
   "list",
   "set"
  };

  // Class
  // note the code does some mapping as well:
  // bradley = apc
  // scientist = npc
  // heli = helicopter
  // murderer = zombie  (I know they are different but how often to do use both at the same time?)
  readonly List < string > dclass = new List < string > {
   "chicken",
   "bear",
   "wolf",
   "boar",
   "horse",
   "stag",
   "buildingblock",
   "npc",
   "player",
   "time",
   "zombie",
   "apc",
   "helicopter",
   "building",
   "bgrade"
  };

  // time types
  readonly List < string > ttype = new List < string > {
  "0",
  "1",
  "2",
  "3",
  "4",
  "5",
  "6",
  "7",
  "8",
  "9",
  "10",
  "11",
  "12",
  "13",
  "14",
  "15",
  "16",
  "17",
  "18",
  "19",
  "20",
  "21",
  "22",
  "23"
  };

  // bgrade types
  readonly List < string > bgtype = new List < string > {
  "Twigs",
  "Wood",
  "Stone",
  "Metal",
  "TopTier"
  };
  
  // building material types
  readonly List < string > btype = new List < string > {
   "deployed",
   "door",
   "floor",
   "foundation",
   "highexternal",
   "other",
   "roof",
   "stairs",
   "toolcupboard",
   "wall"
  };

  // damage types (some seem rather redundant, go FacePunch)
  // this order matched the HitInfo, do not touch or it will break the list command
  readonly List < string > dtype = new List < string > {
   "generic",
   "hunger",
   "thirst",
   "cold",
   "drowned",
   "heat",
   "bleeding",
   "poison",
   "suicide",
   "bullet",
   "slash",
   "blunt",
   "fall",
   "radiation",
   "bite",
   "stab",
   "explosion",
   "radiationexposure",
   "coldexposure",
   "decay",
   "electricshock",
   "arrow"
  };

  // deployables list
  List < string > deployable_list = new List< string >();

  // max size of damage types, if this changed the dtype above needs to be updated
  private
  const int DamageTypeMax = (int) DamageType.LAST;

  // arrays of multipliers by class and one with zero to make buidlings immuned
  float[] _Zeromultipliers = new float[DamageTypeMax]; // immuned, zero damage

  // Animals
  float[] _Bearmultipliers = new float[DamageTypeMax]; // Bear
  float[] _Boarmultipliers = new float[DamageTypeMax]; // Boar
  float[] _Chickenmultipliers = new float[DamageTypeMax]; // Chicken
  float[] _Horsemultipliers = new float[DamageTypeMax]; // Horse
  float[] _Stagmultipliers = new float[DamageTypeMax]; // Stag
  float[] _Wolfmultipliers = new float[DamageTypeMax]; // Wolf

  float[] _Buildingmultipliers = new float[DamageTypeMax]; // Buildings
  float[] _Zombiemultipliers = new float[DamageTypeMax]; // Murderer (Halloween) and Zombies
  float[] _Playermultipliers = new float[DamageTypeMax]; // Players
  float[] _NPCmultipliers = new float[DamageTypeMax]; // Scientists and NPCs
  float[] _APCmultipliers = new float[DamageTypeMax]; // APC aka Bradley
  float[] _Helimultipliers = new float[DamageTypeMax]; // Helicopter

  // time multiplier
  float[] _Timemultipliers = new float[24]; // Per Hour Multiplier

  // bgrade multipliers
  float _TwigsMultiplier  = 1.0F; // Twigs Multiplier
  float _WoodMultiplier  = 1.0F; // Wood Multiplier
  float _StoneMultiplier = 1.0F; // Stone Multiplier
  float _MetalMultiplier = 1.0F; // Metal Multiplier
  float _TopTierMultiplier = 1.0F; // TopTier Multiplier
  
  // to indicate I need to update the json file
  bool _didConfigChange;

  void Init() {
   if (!permission.PermissionExists(permAdmin)) permission.RegisterPermission(permAdmin, this);
   // LoadDefaultMessages();  // Done Automatically
   LoadConfigValues();
   build_dep_list();
  }

  void LoadDefaultMessages() {
   // English
   lang.RegisterMessages(new Dictionary < string, string > {
    // general messages
    ["help"] = "You can use list to show a setting and set to set setting.  For example /dc list building door or /dc set npc arrow 2 .",
    ["nopermission"] = "You do not have permission to use that command.",
    ["wrongsyntax"] = "Incorrect Syntax used, please specify help, list or set and then the parameters for those actions.",
    ["wrongsyntaxList"] = "Incorrect Syntax used for action List. Parameters are optionally: Class, Type.",
    ["wrongsyntaxSet"] = "Incorrect Syntax used for action Set. Parameters are: Class, Type, Value.",
    ["wrongaction"] = "Action can be Help, List or Set.",
    ["wrongclass"] = "Class can only be set to one of Bear, Boar, Chicken, Horse, Stag, Wolf, APC (or Bradley), BGrade, Building, BuildingBlock, Player, Heli, NPC (which includes scientists) , Zombie (which includes Murderers).",
    ["wrongbtype"] = "That is not a supported type: foundation, wall, floor, door, stair, roof, highexternal, other.",
    ["wrongttype"] = "That is not a supported type: 0 through 23.",
    ["wrongbgtype"] = "That is not a supported type: twigs, wood, stone, metal, toptier. (or 0-4)",
    ["wrongtype"] = "That is not a supported type: Arrow, Bite, Bleeding, Blunt, Bullet, Cold, ColdExposure, Decay, Drowned, ElectricShock, Explosion, Fall, Generic, Heat, Hunger, Poison, Radiation, RadiationExposure, Slash, Stab, Suicide, Thirst.",
    ["wrongbvalues"] = "Building Values can only be set to true or false.",
    ["wrongnvalues"] = "Multiplier Values can only be set from 0 to 100.00.",
    ["frontmess"] = "You have set",
    ["bmiddlemess"] = "protection to",
    ["middlemess"] = "to",
    ["endmess"] = ".",
    // Building Types
    ["door"] = "Doors",
    ["floor"] = "Floors",
    ["foundation"] = "Foundations",
    ["other"] = "Other Building Materials",
    ["roof"] = "Roofs",
    ["stairs"] = "Stairs",
    ["wall"] = "Walls",
	["toolcupboard"] = "ToolCupboard",
	["deployed"] = "Deployable",
	["highexternal"] = "High External",
    // Class
    ["apc"] = "APC aka Bradley",
    ["bear"] = "bear",
    ["boar"] = "boar",
    ["building"] = "Building",
    ["buildingblock"] = "Building Block",
    ["chicken"] = "Chicken",
    ["heli"] = "Helicopter",
    ["horse"] = "Horse",
    ["npc"] = "NPC aka Scientist",
    ["player"] = "Player",
    ["stag"] = "Stag",
	["time"] = "Time",
	["bgrade"] = "Build Grade",
    ["wolf"] = "Wolf",
    ["zombie"] = "Zombie and Murderer",
    ["murderer"] = "Zombie and Murderer",
    ["scientist"] = "NPC aka Scientist",
    // Damage Types
    ["arrow"] = "Arrow",
    ["bite"] = "Bite",
    ["bleeding"] = "Bleeding",
    ["blunt"] = "Blunt",
    ["bullet"] = "Bullet",
    ["cold"] = "Cold",
    ["coldexposure"] = "Cold Exposure",
    ["decay"] = "Decay",
    ["drowned"] = "Drowned",
    ["electricshock"] = "Electric Shock",
    ["explosion"] = "Explosion",
    ["fall"] = "Fall",
    ["generic"] = "Generic",
    ["heat"] = "Heat",
    ["hunger"] = "Hunger",
    ["poison"] = "Poison",
    ["radiation"] = "Radiation",
    ["radiationexposure"] = "Radiation Exposure",
    ["slash"] = "Slash",
    ["stab"] = "Stab",
    ["suicide"] = "Suicide",
    ["thirst"] = "Thirst",
    // Multiplier headings
    ["multipliers"] = "Multipliers"
   }, this);
  }

	void build_dep_list()
    {
        foreach (var itemDef in ItemManager.GetItemDefinitions().ToList())
             {
                var mod = itemDef.GetComponent<ItemModDeployable>();
                if (mod != null)
				{
					if (itemDef.name.LastIndexOf(".item") > 0)
					{
						deployable_list.Add(itemDef.name.Substring(0,itemDef.name.LastIndexOf(".item")).Replace("_","."));
						deployable_list.Add(itemDef.name.Substring(0,itemDef.name.LastIndexOf(".item")).Replace("_",".")+".deployed"); // hack to deal with some having deployed and some not
					}
					else
					{
						deployable_list.Add(itemDef.name.Replace("_","."));
						deployable_list.Add(itemDef.name.Replace("_",".")+".deployed");  // hack to deal with some having deployed and some not
					}
				}
             }
		// deal with messed up repair_bench losing its "_" to become repairbench
		deployable_list.Add("repairbench.deployed");

		// debugging dump
		//foreach (string p in deployable_list)
        //{
        //    PrintWarning(p);
        //}
    }


  void Loaded() => LoadConfigValues();
  protected override void LoadDefaultConfig() => Puts("New configuration file created.");

  void LoadConfigValues() {
   foreach(DamageType val in Enum.GetValues(typeof(DamageType))) {
    if (val == DamageType.LAST) continue;
    _APCmultipliers[(int) val] = Convert.ToSingle(GetConfigValue("APC_Multipliers", val.ToString().ToLower(), 1.0));
    _Bearmultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Bear_Multipliers", val.ToString().ToLower(), 1.0));
    _Boarmultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Boar_Multipliers", val.ToString().ToLower(), 1.0));
    _Buildingmultipliers[(int) val] = Convert.ToSingle(GetConfigValue("BuildingBlock_Multipliers", val.ToString().ToLower(), 1.0));
    _Chickenmultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Chicken_Multipliers", val.ToString().ToLower(), 1.0));
    _Helimultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Heli_Multipliers", val.ToString().ToLower(), 1.0));
    _Horsemultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Horse_Multipliers", val.ToString().ToLower(), 1.0));
    _NPCmultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Scientist_Multipliers", val.ToString().ToLower(), 1.0));
    _Playermultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Player_Multipliers", val.ToString().ToLower(), 1.0));
    _Stagmultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Stag_Multipliers", val.ToString().ToLower(), 1.0));
    _Wolfmultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Wolf_Multipliers", val.ToString().ToLower(), 1.0));
    _Zombiemultipliers[(int) val] = Convert.ToSingle(GetConfigValue("Zombie_Multipliers", val.ToString().ToLower(), 1.0)); // also murderers
    _Zeromultipliers[(int) val] = 0;
   }

   for (var i = 0; i < 24; i++) {
	   // Puts(i.ToString());
       _Timemultipliers[(int) i] = Convert.ToSingle(GetConfigValue("Time_Multipliers", i.ToString(), 1.0)); // time in hours
   }

   ProtectFoundation = Convert.ToBoolean(GetConfigValue("Building", "ProtectFoundation", "false"));
   ProtectFloor = Convert.ToBoolean(GetConfigValue("Building", "ProtectFloor", "false"));
   ProtectRoof = Convert.ToBoolean(GetConfigValue("Building", "ProtectRoof", "false"));
   ProtectWall = Convert.ToBoolean(GetConfigValue("Building", "ProtectWall", "false"));
   ProtectStairs = Convert.ToBoolean(GetConfigValue("Building", "ProtectStairs", "false"));
   ProtectDoor = Convert.ToBoolean(GetConfigValue("Building", "ProtectDoor", "false"));
   ProtectOther = Convert.ToBoolean(GetConfigValue("Building", "ProtectOther", "false"));
   ProtectDeployed = Convert.ToBoolean(GetConfigValue("Building", "ProtectDeployed", "false"));
   ProtectTC = Convert.ToBoolean(GetConfigValue("Building", "ProtectToolCupboard", "false"));
   ProtectHighExternal = Convert.ToBoolean(GetConfigValue("Building", "ProtectHighExternal", "false"));

   _TwigsMultiplier   = Convert.ToSingle(GetConfigValue("Building_Grade_Multipliers", "Twigs",   1.0));
   _WoodMultiplier    = Convert.ToSingle(GetConfigValue("Building_Grade_Multipliers", "Wood",    1.0));
   _StoneMultiplier   = Convert.ToSingle(GetConfigValue("Building_Grade_Multipliers", "Stone",   1.0));
   _MetalMultiplier   = Convert.ToSingle(GetConfigValue("Building_Grade_Multipliers", "Metal",   1.0));
   _TopTierMultiplier = Convert.ToSingle(GetConfigValue("Building_Grade_Multipliers", "TopTier", 1.0));

   if (!_didConfigChange) return;
   Puts("Configuration file updated.");
   SaveConfig();
  }

  object GetConfigValue(string category, string setting, object defaultValue) {
   var data = Config[category] as Dictionary < string, object > ;
   object value;
   if (data == null) {
    data = new Dictionary < string, object > ();
    Config[category] = data;
    _didConfigChange = true;
   }

   if (data.TryGetValue(setting, out value)) return value;
   value = defaultValue;
   data[setting] = value;
   _didConfigChange = true;
   return value;
  }

  object SetConfigValue(string category, string setting, object defaultValue) {
   var data = Config[category] as Dictionary < string, object > ;
   object value;

   if (data == null) {
    data = new Dictionary < string, object > ();
    Config[category] = data;
    _didConfigChange = true;
   }

   value = defaultValue;
   data[setting] = value;
   _didConfigChange = true;
   return value;
  }

  [Command("DamageControl", "damagecontrol", "damcon", "dc")]
  void chatCommand_DamageControl(IPlayer player, string command, string[] args) {
   string paramaaction = null;
   string paramaclass = null;
   string paramatype = null;
   string paramavalue = null;
   Boolean newbool = false;
   float newnumber = -1;

   if (!IsAllowed(player))
   {
    player.Reply(Lang("nopermission", player.Id, command));
   }
   else
   {
    if (args == null || args.Length < 1)
	{
     player.Reply(Lang("wrongsyntax", player.Id));
     return;
    }
	else
	{
     paramaaction = args[0].ToLower();
     if (!dcaction.Contains(paramaaction))
	 {
      player.Reply(Lang("wrongaction", player.Id, args[0]));
      return;
     }
	 if (paramaaction == "help")
	 {
      player.Reply(Lang("help", player.Id, args[0]));
      return;
     }
     else if (paramaaction == "set" && args.Length != 4)
	 {
	  player.Reply(Lang("wrongsyntaxSet", player.Id));
      return;
	 }
     else if (paramaaction == "list" && args.Length < 2 )
	 {
      player.Reply(Lang("wrongsyntaxList", player.Id));
      return;
     }
	 else
	 {
      if (args.Length > 1)
	  {
		paramaclass = args[1].ToLower();
		if (paramaclass == "build")
		   paramaclass = "building";
	   else if (paramaclass.Contains("brad"))
		   paramaclass = "apc";
	   else if (paramaclass.Contains("murder"))
		   paramaclass = "zombie";
	   else if (paramaclass.Contains("heli"))
		   paramaclass = "helicopter";
	   else if (paramaclass == "science")
		   paramaclass = "npc";
	   }
      else
       paramaclass = null;
      if (args.Length > 2)
	  {
		paramatype = args[2].ToLower();
		if (paramatype == "stair")
			paramatype = "stairs";
	  }
      else
       paramatype = null;
      if (args.Length > 3)
       paramavalue = args[3].ToLower();
      else
       paramavalue = null;
     }

     if (paramaaction == "set" && paramaclass == null) {
      player.Reply(Lang("wrongclass", player.Id, args[1]));
      return;
     }
     if (paramaclass != null && paramaclass != "" && !dclass.Contains(paramaclass)) {
      player.Reply(Lang("wrongclass", player.Id, args[1]));
      return;
     }
     if (paramavalue == "1" && paramaclass.Contains("build") && !paramaclass.Contains("block")) {
      paramavalue = "true";
     } else if (paramavalue == "0" && paramaclass.Contains("build") && !paramaclass.Contains("block")) {
      paramavalue = "false";
     }
     if (paramavalue != "true" && paramavalue != "false" && paramavalue != null)
      try {
       newnumber = Convert.ToSingle(paramavalue);
      } catch (FormatException) {
       player.Reply(Lang("wrongnvalues", player.Id, args[3]));
       return;
      } catch (OverflowException) {
       player.Reply(Lang("wrongnvalues", player.Id, args[3]));
       return;
      }
     if (paramaclass.Contains("build") && !paramaclass.Contains("block"))
	 {
		if (paramavalue != "true" && paramavalue != "false" && paramaaction == "set")
		{
			player.Reply(Lang("wrongbvalues", player.Id, args[3]));
			return;
		}
     } else if ((newnumber < 0 || newnumber > 100) && paramaaction == "set") {
      player.Reply(Lang("wrongnvalues", player.Id, args[3]));
      return;
     }
     if (paramaaction == "set" || paramatype != null) {
		// change text to boolean
		if (paramavalue == "true") {
		newbool = true;
		} else if (paramavalue == "false") {
		newbool = false;
		}

		if (paramaclass.Contains("time")) {
			// check type values
			if (!ttype.Contains(paramatype)) {
				player.Reply(Lang("wrongttype", player.Id, args[2]));
				return;
			}
		} else if (paramaclass.Contains("bgrade")) {
			// Puts("before paramatype: " + paramatype);
			// convert numbers to names
			if (paramatype == "0" || paramatype.Contains("twig"))
				paramatype = "Twigs";
			else if (paramatype == "1" || paramatype.Contains("wood"))
				paramatype = "Wood";
			else if (paramatype == "2" || paramatype.Contains("stone"))
				paramatype = "Stone";
			else if (paramatype == "3" || paramatype.Contains("metal"))
				paramatype = "Metal";
			else if (paramatype == "4" || paramatype.Contains("toptier"))
				paramatype = "TopTier";
			// Puts("After paramatype: " + paramatype);
			
			// check type values
			if (!bgtype.Contains(paramatype)) {
				player.Reply(Lang("wrongbgtype", player.Id, args[2]));
				return;
			}
		} else if (!paramaclass.Contains("bgrade")  && (!paramaclass.Contains("build") || paramaclass.Contains("block"))) {
			// check type values
			if (!dtype.Contains(paramatype)) {
				player.Reply(Lang("wrongtype", player.Id, args[2]));
				return;
			}
		} else {
			// check type values
			if (!btype.Contains(paramatype)) {
				player.Reply(Lang("wrongbtype", player.Id, args[2]));
				return;
			}
		}
	 }
	 
	 if (paramaaction == "set")
	 {
		if (paramaclass.Contains("bgrade"))
		{
			SetConfigValue("Building_Grade_Multipliers", paramatype, newnumber);
		}
		else if (paramaclass.Contains("build") && !paramaclass.Contains("block")) {
		if (paramatype.Contains("found")) {
			ProtectFoundation = newbool;
			SetConfigValue("Building", "ProtectFoundation", paramavalue);
		} else if (paramatype.Contains("floor")) {
			ProtectFloor = newbool;
			SetConfigValue("Building", "ProtectFloor", paramavalue);
		} else if (paramatype.Contains("door")) {
			ProtectDoor = newbool;
			SetConfigValue("Building", "ProtectDoor", paramavalue);
		} else if (paramatype.Contains("highexternal")) {
			ProtectHighExternal = newbool;
			SetConfigValue("Building", "ProtectHighExternal", paramavalue);
		} else if (paramatype.Contains("wall")) {
			ProtectWall = newbool;
			SetConfigValue("Building", "ProtectWall", paramavalue);
		} else if (paramatype.Contains("stair")) {
			ProtectStairs = newbool;
			SetConfigValue("Building", "ProtectStairs", paramavalue);
		} else if (paramatype.Contains("roof")) {
			ProtectRoof = newbool;
			SetConfigValue("Building", "ProtectRoof", paramavalue);
		} else if (paramatype.Contains("other")) {
			ProtectOther = newbool;
			SetConfigValue("Building", "ProtectOther", paramavalue);
		} else if (paramatype.Contains("deploy")) {
			ProtectDeployed = newbool;
			SetConfigValue("Building", "ProtectDeployed", paramavalue);
		} else if (paramatype.Contains("cupboard")) {
			ProtectTC = newbool;
			SetConfigValue("Building", "ProtectToolCupboard", paramavalue);
		}
	
		} else if (paramaclass.Contains("build") && paramaclass.Contains("block")) {
		SetConfigValue("BuildingBlock_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("apc") || paramaclass.Contains("bradley")) {
		SetConfigValue("APC_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("heli")) {
		SetConfigValue("Heli_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("npc") || paramaclass.Contains("scientist")) {
		SetConfigValue("Scientist_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("zombie") || paramaclass.Contains("murderer")) {
		SetConfigValue("Zombie_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("player")) {
		SetConfigValue("Player_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("bear")) {
		SetConfigValue("Bear_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("boar")) {
		SetConfigValue("Boar_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("chicken")) {
		SetConfigValue("Chicken_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("horse")) {
		SetConfigValue("Horse_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("stag")) {
		SetConfigValue("Stag_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("time")) {
		SetConfigValue("Time_Multipliers", paramatype, newnumber);
		} else if (paramaclass.Contains("wolf")) {
		SetConfigValue("Wolf_Multipliers", paramatype, newnumber);
		}
		SaveConfig();
		if (paramavalue != "true" && paramavalue != "false") {
		player.Reply(Lang("frontmess", player.Id) + " " + Lang(paramaclass, player.Id) + " " + Lang(paramatype, player.Id) + " " + Lang("multipliers", player.Id) + " " +
			Lang("middlemess", player.Id) + " " + newnumber.ToString("G4") + " " + Lang("endmess"));
		} else {
		player.Reply(Lang("frontmess", player.Id) + " " + Lang(paramaclass, player.Id) + " " + Lang(paramatype, player.Id) + " " +
						Lang("bmiddlemess", player.Id) + " " + paramavalue + " " + Lang("endmess", player.Id));
		}

     } else // list
     {
		if (paramaclass != null && paramatype != null) // dump a type per class
		{
		 printvalue(player, paramaclass, paramatype, getHitScale(paramaclass,paramatype.ToLower()));
		}
		else if (paramaclass != null) // dump a class
		{
			if (paramaclass.Contains("bgrade"))
				for (var i = 0; i < bgtype.Count; i++) {
					printvalue(player, paramaclass, bgtype[i], getHitScale(paramaclass,bgtype[i].ToLower()));
				}
			else if (paramaclass.Contains("build") && !paramaclass.Contains("block"))
				for (var i = 0; i < btype.Count; i++) {
					printvalue(player, paramaclass, btype[i], getHitScale(paramaclass,btype[i].ToLower()));
				}
			else
				for (var i = 0; i < DamageTypeMax; i++) {
					printvalue(player, paramaclass, dtype[i], getHitScale(paramaclass,dtype[i].ToLower()));
				}
		}
		// this is just too long to read in game so I have removed it
		//else // dump all
		//{
		//	// Buildings
		//	for (var k = 0; k < btype.Count; k++) {
		//			printvalue(player, paramaclass, btype[k], getHitScale(paramaclass,btype[k]));
		//		}
		//	// all other types
		//	for (var i = 0; i < dclass.Count-1; i++) {
		//		for (var j = 0; j < DamageTypeMax; j++) {
		//			printvalue(player, dclass[i], dtype[j], getHitScale(paramaclass,dtype[j]));
		//		}
		//	}
		//}
     }
    }
   }
   return;
  }

    void printvalue(IPlayer player, string paramaclass, string paramatype, string paravalue) {
	if (paramaclass.Contains("build") && !paramaclass.Contains("block"))
		 {
			player.Reply(Lang("frontmess", player.Id) + " " + Lang(paramaclass, player.Id) + " " + Lang(paramatype, player.Id) + " " +
	                Lang("bmiddlemess", player.Id) + " " + paravalue + " " + Lang("endmess", player.Id));
		 }
		else
		 {
			 player.Reply(Lang("frontmess", player.Id) + " " + Lang(paramaclass, player.Id) + " " + Lang(paramatype, player.Id) + " " + Lang("multipliers", player.Id) + " " +
								Lang("middlemess", player.Id) + " " + paravalue + " " + Lang("endmess"));
		 }
	}


    string getHitScale(string paramaclass, string paramatype) {

	float  tempnumber = -1;
	string tempstring = "Undefined";

	if (paramaclass.Contains("build") && !paramaclass.Contains("block")) {
       if (paramatype.Contains("found")) {
			tempstring = Convert.ToString(ProtectFoundation);
       } else if (paramatype.Contains("floor")) {
			tempstring = Convert.ToString(ProtectFloor);
       } else if (paramatype.Contains("door")) {
			tempstring = Convert.ToString(ProtectDoor);
       } else if (paramatype.Contains("wall")) {
			tempstring = Convert.ToString(ProtectWall);
       } else if (paramatype.Contains("stair")) {
			tempstring = Convert.ToString(ProtectStairs);
       } else if (paramatype.Contains("roof")) {
			tempstring = Convert.ToString(ProtectRoof);
       } else if (paramatype.Contains("other")) {
			tempstring = Convert.ToString(ProtectOther);
       } else if (paramatype.Contains("deployed")) {
			tempstring = Convert.ToString(ProtectDeployed);
       }  else if (paramatype.Contains("highexternal")) {
			tempstring = Convert.ToString(ProtectHighExternal);
       }  else if (paramatype.Contains("cupboard")) {
			tempstring = Convert.ToString(ProtectTC);
       }
	}
	else
	{
		if (paramaclass.Contains("build") && paramaclass.Contains("block")) {
			tempnumber = 1; // _Buildingmultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("apc") || paramaclass.Contains("bradley")) {
			tempnumber = _APCmultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("heli")) {
			tempnumber = _Helimultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("npc") || paramaclass.Contains("scientist")) {
			tempnumber = _NPCmultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("zombie") || paramaclass.Contains("murderer")) {
			tempnumber = _Zombiemultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("player")) {
			tempnumber = _Playermultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("bear")) {
			tempnumber = _Bearmultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("boar")) {
			tempnumber = _Boarmultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("chicken")) {
			tempnumber = _Chickenmultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("horse")) {
			tempnumber = _Horsemultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("stag")) {
			tempnumber = _Stagmultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("time")) {
			tempnumber = _Timemultipliers[ttype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("wolf")) {
			tempnumber = _Wolfmultipliers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("bgrade")) {
			if (paramatype == "twigs" || paramatype == "0")
				tempnumber = _TwigsMultiplier;
			else if (paramatype == "wood" || paramatype == "1")
				tempnumber = _WoodMultiplier;
			else if (paramatype == "stone" || paramatype == "2")
				tempnumber = _StoneMultiplier;
			else if (paramatype == "metal" || paramatype == "3")
				tempnumber = _MetalMultiplier;
			else if (paramatype == "toptier" || paramatype == "4")
				tempnumber = _TopTierMultiplier;
		}
		tempstring = tempnumber.ToString();
	}
	return tempstring;
  }

  void setHitScale(HitInfo hitInfo, float[] _multipliers, float addlnmod) {

	int   time      = 0;
	float timemod   = 0F;
	
	time = Convert.ToInt32(Math.Floor(TOD_Sky.Instance.Cycle.Hour));

	// make sure this is in range
	if (time > 23 || time < 0)
		time = 0;

	// Puts (time.ToString());
	// get the time of day multiplier
	timemod = _Timemultipliers[time];
	// Puts (timemod.ToString());
	//Puts ("addlnmod: " + addlnmod.ToString());

	for (var i = 0; i < DamageTypeMax; i++) {
		hitInfo.damageTypes.Scale((DamageType) i, _multipliers[i] * timemod * addlnmod);
		// Puts (_multipliers[i].ToString());
	}
  }

  void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo) {

	// debugging
	// PrintWarning("0 " + entity.ShortPrefabName.Replace("_","."));
		
	if (entity == null || hitInfo == null)
		{
			return; // Nothing to process
		}
	else if (entity is NPCMurderer) // Murderer (treated the same as zombies)
		{
			setHitScale(hitInfo, _Zombiemultipliers,1.0F);
		}
	else if (entity is NPCPlayerApex || entity is NPCPlayer) // BotSpawn type Scientists, etc.
		{
			setHitScale(hitInfo, _NPCmultipliers,1.0F);
		}
	else if (entity as BasePlayer != null)
		{
			setHitScale(hitInfo, _Playermultipliers,1.0F);
		}
	else if (entity as BradleyAPC != null) // APC
		{
			setHitScale(hitInfo, _APCmultipliers,1.0F);
		}
	else if (entity as BaseHelicopter != null) // Heli
		{
			setHitScale(hitInfo, _Helimultipliers,1.0F);
		}
	else if (entity as BaseNpc != null)
		{
			if (entity.ShortPrefabName == "zombie") // Zombie
				{
				setHitScale(hitInfo, _Zombiemultipliers,1.0F);
				}
			else if (entity.ShortPrefabName == "bear") // Bear
				{
				setHitScale(hitInfo, _Bearmultipliers,1.0F);
				}
			else if (entity.ShortPrefabName == "boar") // Boar
				{
				setHitScale(hitInfo, _Boarmultipliers,1.0F);
				}
			else if (entity.ShortPrefabName == "chicken") // Chicken
				{
				setHitScale(hitInfo, _Chickenmultipliers,1.0F);
				}
			else if (entity.ShortPrefabName == "horse") // Horse
				{
				setHitScale(hitInfo, _Horsemultipliers,1.0F);
				}
			else if (entity.ShortPrefabName == "stag") // Stag
				{
				setHitScale(hitInfo, _Stagmultipliers,1.0F);
				}
			else if (entity.ShortPrefabName == "wolf") // Wolf
				{
				setHitScale(hitInfo, _Wolfmultipliers,1.0F);
				}
			else // Animal not found
				{
				Puts ("Animal not found in Damage Control: " + entity.ShortPrefabName + " using Bear");
				setHitScale(hitInfo, _Bearmultipliers,1.0F);
				}
		}
	// special overrides for building
	else if (entity is BuildingBlock || entity is Door || entity.ShortPrefabName.Contains("external"))
	{
		if ((entity.ShortPrefabName.Contains("foundation") && ProtectFoundation == true) ||
		(entity.ShortPrefabName.Contains("external") && ProtectHighExternal == true) ||
		(entity.ShortPrefabName.Contains("wall") && !(entity is Door) && !(entity.ShortPrefabName.Contains("external")) && ProtectWall == true) ||
		(entity.ShortPrefabName.Contains("floor") && ProtectFloor == true) ||
		(entity.ShortPrefabName.Contains("roof") && ProtectRoof == true) ||
		((entity is Door || entity.ShortPrefabName.Contains("hatch")) && !entity.ShortPrefabName.Contains("external") && ProtectDoor == true) ||
		((entity.ShortPrefabName.Contains("stairs") || entity.ShortPrefabName.Contains("hatch")) && ProtectStairs == true) ||
		(entity is BuildingBlock && !entity.ShortPrefabName.Contains("foundation") && !entity.ShortPrefabName.Contains("wall") &&
		!entity.ShortPrefabName.Contains("floor") && !entity.ShortPrefabName.Contains("roof") && !entity.ShortPrefabName.Contains("hatch") &&
		!entity.ShortPrefabName.Contains("external") &&
		!(entity is Door) && ProtectOther == true))
		{
			setHitScale(hitInfo, _Zeromultipliers,1.0F);
		}
		else if (deployable_list.Contains(entity.ShortPrefabName.Replace("_",".")) && ProtectDeployed == true)  // this deal with high walls etc.
		{
			setHitScale(hitInfo, _Zeromultipliers,1.0F);
		}
		else if (entity is BuildingBlock)
		{
			BuildingBlock buildingBlock = entity as BuildingBlock;
			//Puts("buildingBlock.grade: " + buildingBlock.grade);
			if (buildingBlock.grade == null)
				setHitScale(hitInfo, _Buildingmultipliers,1.0F);
			else if (buildingBlock.grade == BuildingGrade.Enum.Twigs)
				setHitScale(hitInfo, _Buildingmultipliers, _TwigsMultiplier);
			else if (buildingBlock.grade == BuildingGrade.Enum.Wood)
				setHitScale(hitInfo, _Buildingmultipliers, _WoodMultiplier);
			else if (buildingBlock.grade == BuildingGrade.Enum.Stone)
				setHitScale(hitInfo, _Buildingmultipliers, _StoneMultiplier);
			else if (buildingBlock.grade == BuildingGrade.Enum.Metal)
				setHitScale(hitInfo, _Buildingmultipliers, _MetalMultiplier);
			else if (buildingBlock.grade == BuildingGrade.Enum.TopTier)
				setHitScale(hitInfo, _Buildingmultipliers, _TopTierMultiplier);
		}
		else
			setHitScale(hitInfo, _Buildingmultipliers,1.0F);

   }
	else if (deployable_list.Contains(entity.ShortPrefabName.Replace("_","."))) // Deployed  or TC
		{
			if (entity.ShortPrefabName.Contains("cupboard.tool.deployed") && ProtectTC == true) // Tool Cupboard
			{
				setHitScale(hitInfo, _Zeromultipliers,1.0F);
			}
			else if (ProtectDeployed == true)  // deployables
			{
				setHitScale(hitInfo, _Zeromultipliers,1.0F);
			}
		}
   else
	   return; // Nothing to process
  }

  bool IsAllowed(IPlayer player) {
   return player != null && (player.IsAdmin || player.HasPermission(permAdmin));
  }

  T GetConfig < T > (string name, T value) => Config[name] == null ? value : (T) Convert.ChangeType(Config[name], typeof(T));
  string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

 }
}