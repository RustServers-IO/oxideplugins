using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;

namespace Oxide.Plugins {
 [Info("DamageControl", "ColonBlow, MSpeedie", "2.1.5", ResourceId = 0)]
 [Description("Allows authorized Users to control damage settings for animals, buildings, npcs, players and zombies")]
 // internal class DamageControl : RustPlugin
 class DamageControl: CovalencePlugin {
  // note we check isAdmin as well so Admins get this by default
  readonly string permAdmin = "damagecontrol.admin";

  // toggles to make these immuned to damage if true
  public bool ProtectFoundation;
  public bool ProtectFloor;
  public bool ProtectRoof;
  public bool ProtectWall;
  public bool ProtectStairs;
  public bool ProtectDoor;
  public bool ProtectOther;

  // these are to make checking look cleaner
  // action
  readonly List < string > dcaction = new List < string > {
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
   "animal",
   "buildingblock",
   "npc",
   "player",
   "zombie",
   "apc",
   "helicopter",
   "building"
  };

  // building material types
  readonly List < string > btype = new List < string > {
   "foundation",
   "wall",
   "floor",
   "door",
   "stairs",
   "roof",
   "other"
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

  // max size of damage types, if this changed the dtype above needs to be updated
  private
  const int DamageTypeMax = (int) DamageType.LAST;

  // arrays of modifiers by class and one with zero to make buidlings immuned
  private float[] _Zeromodifiers = new float[DamageTypeMax]; // immuned, zero damage
  private float[] _Animalmodifiers = new float[DamageTypeMax]; // Animals
  private float[] _Buildingmodifiers = new float[DamageTypeMax]; // Buildings
  private float[] _Zombiemodifiers = new float[DamageTypeMax]; // Murderer (Halloween) and Zombies
  private float[] _Playermodifiers = new float[DamageTypeMax]; // Players
  private float[] _NPCmodifiers = new float[DamageTypeMax]; // Scientists and NPCs
  private float[] _APCmodifiers = new float[DamageTypeMax]; // APC aka Bradley
  private float[] _Helimodifiers = new float[DamageTypeMax]; // Helicopter


  // to indicate I need to update the json file
  private bool _didConfigChange;

  void Init() {
   permission.RegisterPermission(permAdmin, this);
   LoadDefaultMessages();
   LoadConfigValues();
  }

  void LoadDefaultMessages() {
   // English
   lang.RegisterMessages(new Dictionary < string, string > {
    // general messages
    ["nopermission"] = "You do not have permission to use that command.",
    ["wrongsyntax"] = "Incorrect Syntax used, please specify list or set and then the parameters for those actions.",
    ["wrongsyntaxList"] = "Incorrect Syntax used for action List. Parameters are optionally: Class, Type.",
    ["wrongsyntaxset"] = "Incorrect Syntax used for action set. Parameters are: Class, Type, Value.",
    ["wrongaction"] = "Action can be List or Set.",
    ["wrongclass"] = "Class can only be set to one of Animal, APC (or Bradley), Building, BuildingBlock, Player, Heli, NPC (which includes scientists) , Zombie (which includes Murderers).",
    ["wrongbtype"] = "That is not a supported type: foundation, wall, floor, door, stair, roof, other.",
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
    ["other"] = "Other Buidling Materials",
    ["roof"] = "Roofs",
    ["stairs"] = "Stairs",
    ["wall"] = "Walls",
    // Class
    ["animal"] = "Animal",
    ["apc"] = "APC aka Bradley",
    ["building"] = "Building",
    ["buildingblock"] = "Building Block",
    ["heli"] = "Helicopter",
    ["npc"] = "NPC aka Scientist",
    ["player"] = "Player",
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
    ["suicide"] = "Suicidie",
    ["thirst"] = "Thirst",
    // Multiplier headings
    ["multipliers"] = "Multipliers"
   }, this);
  }

  private void Loaded() => LoadConfigValues();
  protected override void LoadDefaultConfig() => Puts("New configuration file created.");

  private void LoadConfigValues() {
   foreach(DamageType val in Enum.GetValues(typeof(DamageType))) {
    if (val == DamageType.LAST) continue;
    _Animalmodifiers[(int) val] = Convert.ToSingle(GetConfigValue("Animal_Multipliers", val.ToString().ToLower(), 1.0));
    _APCmodifiers[(int) val] = Convert.ToSingle(GetConfigValue("APC_Multipliers", val.ToString().ToLower(), 1.0));
    _Buildingmodifiers[(int) val] = Convert.ToSingle(GetConfigValue("BuildingBlock_Multipliers", val.ToString().ToLower(), 1.0));
    _Helimodifiers[(int) val] = Convert.ToSingle(GetConfigValue("Heli_Multipliers", val.ToString().ToLower(), 1.0));
    _Zombiemodifiers[(int) val] = Convert.ToSingle(GetConfigValue("Zombie_Multipliers", val.ToString().ToLower(), 1.0)); // also murderers
    _Playermodifiers[(int) val] = Convert.ToSingle(GetConfigValue("Player_Multipliers", val.ToString().ToLower(), 1.0));
    _NPCmodifiers[(int) val] = Convert.ToSingle(GetConfigValue("Scientist_Multipliers", val.ToString().ToLower(), 1.0));
    _Zeromodifiers[(int) val] = 0;
   }
   ProtectFoundation = Convert.ToBoolean(GetConfigValue("Building", "ProtectFoundation", "false"));
   ProtectFloor = Convert.ToBoolean(GetConfigValue("Building", "ProtectFloor", "false"));
   ProtectRoof = Convert.ToBoolean(GetConfigValue("Building", "ProtectRoof", "false"));
   ProtectWall = Convert.ToBoolean(GetConfigValue("Building", "ProtectWall", "false"));
   ProtectStairs = Convert.ToBoolean(GetConfigValue("Building", "ProtectStairs", "false"));
   ProtectDoor = Convert.ToBoolean(GetConfigValue("Building", "ProtectDoor", "false"));
   ProtectOther = Convert.ToBoolean(GetConfigValue("Building", "ProtectOther", "false"));

   if (!_didConfigChange) return;
   Puts("Configuration file updated.");
   SaveConfig();
  }

  private object GetConfigValue(string category, string setting, object defaultValue) {
   var data = Config[category] as Dictionary < string,
    object > ;
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

  private object SetConfigValue(string category, string setting, object defaultValue) {
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
   
   if (!IsAllowed(player)) {
    player.Reply(Lang("nopermission", player.Id, command));
   } else {
    if (args == null || args.Length < 1) {
     player.Reply(Lang("wrongsyntax", player.Id));
     return;
    } else {
     paramaaction = args[0].ToLower();
     if (!dcaction.Contains(paramaaction)) {
      player.Reply(Lang("wrongaction", player.Id, args[0]));
      return;
     }
     if ((paramaaction == "set" && args.Length != 4) ||
		 (paramaaction == "list" && args.Length < 2 )) {
      player.Reply(Lang("wrongsyntax", player.Id));
      return;
     } else {
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
     if (paramaclass.Contains("build") && !paramaclass.Contains("block") && paramavalue != "true" && paramavalue != "false" && paramaaction == "set") {
      player.Reply(Lang("wrongbvalues", player.Id, args[3]));
      return;
     } else if ((newnumber < 0 || newnumber > 100) && paramaaction == "set") {
      player.Reply(Lang("wrongnvalues", player.Id, args[3]));
      return;
     }
     if (paramaaction == "set") {
      // change text to boolean
      if (paramavalue == "true") {
       newbool = true;
      } else if (paramavalue == "false") {
       newbool = false;
      }

      if (!paramaclass.Contains("build") || paramaclass.Contains("block")) {
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

      if (paramaclass.Contains("build") && !paramaclass.Contains("block")) {
       if (paramatype.Contains("found")) {
        ProtectFoundation = newbool;
        SetConfigValue("Building", "ProtectFoundation", paramavalue);
       } else if (paramatype.Contains("floor")) {
        ProtectFloor = newbool;
        SetConfigValue("Building", "ProtectFloor", paramavalue);
       } else if (paramatype.Contains("door")) {
        ProtectDoor = newbool;
        SetConfigValue("Building", "ProtectDoor", paramavalue);
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
      } else if (paramaclass.Contains("animal")) {
       SetConfigValue("Animal_Multipliers", paramatype, newnumber);
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
		 printvalue(player, paramaclass, paramatype, getHitScale(paramaclass,paramatype));
		}
		else if (paramaclass != null) // dump a class
		{
			if (paramaclass.Contains("build") && !paramaclass.Contains("block"))
				for (var i = 0; i < btype.Count; i++) {
					printvalue(player, paramaclass, btype[i], getHitScale(paramaclass,btype[i]));
				}
			else
				for (var i = 0; i < DamageTypeMax; i++) {
					printvalue(player, paramaclass, dtype[i], getHitScale(paramaclass,dtype[i]));
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

    private void printvalue(IPlayer player, string paramaclass, string paramatype, string paravalue) {
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


    private string getHitScale(string paramaclass, string paramatype) {

	string tempstring = "Undefined";
	float  tempnumber = -1;
	
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
       }
	}
	else
	{
		if (paramaclass.Contains("build") && paramaclass.Contains("block")) {
			tempnumber =_Buildingmodifiers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("apc") || paramaclass.Contains("bradley")) {
			tempnumber = _APCmodifiers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("heli")) {
			tempnumber = _Helimodifiers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("npc") || paramaclass.Contains("scientist")) {
			tempnumber = _NPCmodifiers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("zombie") || paramaclass.Contains("murderer")) {
			tempnumber = _Zombiemodifiers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("player")) {
			tempnumber = _Playermodifiers[dtype.IndexOf(paramatype)];
		} else if (paramaclass.Contains("animal")) {
			tempnumber = _Animalmodifiers[dtype.IndexOf(paramatype)];
		}
		tempstring = tempnumber.ToString();
	}
	return tempstring;
  }

  private void setHitScale(HitInfo hitInfo, float[] _modifiers) {
   for (var i = 0; i < DamageTypeMax; i++) {
		hitInfo.damageTypes.Scale((DamageType) i, _modifiers[i]);
   }
  }

  private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo) {
   if (entity == null || hitInfo == null) {
    return; // Nothing to process
   } else if (entity as BaseNpc != null && entity.ShortPrefabName != "zombie") // Animals
   {
    setHitScale(hitInfo, _Animalmodifiers);
   } else if ((entity as BasePlayer != null && entity is NPCMurderer) || (entity as BaseNpc != null && entity.ShortPrefabName == "zombie")) // Murderer and Zombies
   {
    setHitScale(hitInfo, _Zombiemodifiers);
   } else if (entity as BasePlayer != null && entity is NPCPlayer) // Scientists, etc.
   {
    setHitScale(hitInfo, _NPCmodifiers);
   } else if (entity as BasePlayer != null) // Player
   {
    setHitScale(hitInfo, _Playermodifiers);
   } else if (entity is BuildingBlock || entity is Door) {
    setHitScale(hitInfo, _Buildingmodifiers);
   } else if (entity as BradleyAPC != null) // APC
   {
    setHitScale(hitInfo, _APCmodifiers);
   } else if (entity as BaseHelicopter != null) // Heli
   {
    setHitScale(hitInfo, _Helimodifiers);
   }

   // special overrides for building
   if (entity is BuildingBlock || entity is Door) {
    if ((entity.name.Contains("foundation") && ProtectFoundation == true) ||
     (entity.name.Contains("wall") && ProtectWall == true) ||
     (entity.name.Contains("floor") && ProtectFloor == true) ||
     (entity.name.Contains("roof") && ProtectRoof == true) ||
     ((entity is Door || entity.name.Contains("hatch")) && ProtectDoor == true) ||
     ((entity.name.Contains("stairs") || entity.name.Contains("hatch")) && ProtectStairs == true) ||
     (entity is BuildingBlock && !entity.name.Contains("foundation") && !entity.name.Contains("wall") &&
      !entity.name.Contains("floor") && !entity.name.Contains("roof") && !entity.name.Contains("hatch") &&
      !(entity is Door) && ProtectOther == true)
    ) {
     setHitScale(hitInfo, _Zeromodifiers);
    } else {
     setHitScale(hitInfo, _Buildingmodifiers);
    }
   }
  }

  bool IsAllowed(IPlayer player) {
   return player != null && (player.IsAdmin || player.HasPermission(permAdmin));
  }

  T GetConfig < T > (string name, T value) => Config[name] == null ? value : (T) Convert.ChangeType(Config[name], typeof(T));
  string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);


 }
}