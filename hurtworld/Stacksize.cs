// Reference: UnityEngine.UI
using Oxide.Core;
using System;
using System.Reflection;
using Assets.Scripts.Core;

namespace Oxide.Plugins
{
	[Info("Stacksize", "Noviets", "1.2.1", ResourceId = 1666)]
	[Description("Stacksize")]

	class Stacksize : HurtworldPlugin
	{
		void OnServerInitialized() => Loaded();
		void Loaded()
		{
			LoadDefaultConfig();

			GlobalItemManager GIM = Singleton<GlobalItemManager>.Instance;
            GIM.GetItem(4).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(4) as IItem, (int)Config["Steak"], null);
            GIM.GetItem(5).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(5) as IItem, (int)Config["Steak"], null);
			GIM.GetItem(6).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(6) as IItem, (int)Config["Steak"], null);
			GIM.GetItem(12).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(12) as IItem, (int)Config["Ruby"], null);
			GIM.GetItem(25).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(25) as IItem, (int)Config["FreshOwrong"], null);
			GIM.GetItem(48).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(48) as IItem, (int)Config["Arrows"], null);
			GIM.GetItem(53).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(53) as IItem, (int)Config["BlastFurnace"], null);
			GIM.GetItem(88).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(88) as IItem, (int)Config["Backpacks"], null);
			GIM.GetItem(90).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(90) as IItem, (int)Config["Backpacks"], null);
			GIM.GetItem(91).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(91) as IItem, (int)Config["ConstructionHammer"], null);
			GIM.GetItem(93).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(93) as IItem, (int)Config["OwnershipStake"], null);
			GIM.GetItem(127).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(127) as IItem, (int)Config["Drills"], null);
			GIM.GetItem(128).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(128) as IItem, (int)Config["Spears"], null);
			GIM.GetItem(144).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(144) as IItem, (int)Config["C4"], null);
			GIM.GetItem(155).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(155) as IItem, (int)Config["Dynamite"], null);
			GIM.GetItem(162).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(162) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(166).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(166) as IItem, (int)Config["Wheels"], null);
			GIM.GetItem(167).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(167) as IItem, (int)Config["Wheels"], null);
			GIM.GetItem(171).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(171) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(172).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(172) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(173).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(173) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(174).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(174) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(175).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(175) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(178).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(178) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(179).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(179) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(180).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(180) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(181).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(181) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(182).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(182) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(183).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(183) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(184).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(184) as IItem, (int)Config["Wheels"], null);
			GIM.GetItem(185).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(185) as IItem, (int)Config["GoatPanels"], null);
			GIM.GetItem(186).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(186) as IItem, (int)Config["GoatPanels"], null);
			GIM.GetItem(187).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(187) as IItem, (int)Config["GoatPanels"], null);
			GIM.GetItem(188).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(188) as IItem, (int)Config["GoatPanels"], null);
			GIM.GetItem(189).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(189) as IItem, (int)Config["GoatPanels"], null);
			GIM.GetItem(190).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(190) as IItem, (int)Config["GoatPanels"], null);
			GIM.GetItem(192).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(192) as IItem, (int)Config["Wheels"], null);
			GIM.GetItem(193).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(193) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(194).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(194) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(195).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(195) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(196).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(196) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(197).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(197) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(198).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(198) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(199).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(199) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(200).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(200) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(201).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(201) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(202).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(202) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(203).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(203) as IItem, (int)Config["CarPanels"], null);
			GIM.GetItem(204).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(204) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(205).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(205) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(206).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(206) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(207).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(207) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(222).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(222) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(223).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(223) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(224).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(224) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(226).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(226) as IItem, (int)Config["Spears"], null);
			GIM.GetItem(227).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(227) as IItem, (int)Config["Spears"], null);
			GIM.GetItem(228).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(228) as IItem, (int)Config["Spears"], null);
			GIM.GetItem(232).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(232) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(233).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(233) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(234).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(234) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(235).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(235) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(236).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(236) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(237).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(237) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(238).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(238) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(239).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(239) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(240).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(240) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(241).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(241) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(242).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(242) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(243).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(243) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(244).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(244) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(245).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(245) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(246).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(246) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(247).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(247) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(248).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(248) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(249).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(249) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(250).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(250) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(251).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(251) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(252).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(252) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(253).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(253) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(254).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(254) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(255).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(255) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(256).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(256) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(257).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(257) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(258).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(258) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(259).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(259) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(260).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(260) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(261).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(261) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(262).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(262) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(263).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(263) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(264).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(264) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(265).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(265) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(266).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(266) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(267).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(267) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(268).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(268) as IItem, (int)Config["Paints"], null);
			GIM.GetItem(273).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(273) as IItem, (int)Config["LandcrabMine"], null);
			GIM.GetItem(274).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(274) as IItem, (int)Config["PoisonTrap"], null);
			GIM.GetItem(276).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(276) as IItem, (int)Config["Backpacks"], null);
			GIM.GetItem(277).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(277) as IItem, (int)Config["Backpacks"], null);
			GIM.GetItem(296).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(296) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(297).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(297) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(298).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(298) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(299).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(299) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(300).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(300) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(301).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(301) as IItem, (int)Config["CarParts"], null);
			GIM.GetItem(304).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(304) as IItem, (int)Config["Wrench"], null);
			GIM.GetItem(305).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(305) as IItem, (int)Config["Wheels"], null);
			GIM.GetItem(306).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(306) as IItem, (int)Config["Wheels"], null);
			GIM.GetItem(307).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(307) as IItem, (int)Config["Drills"], null);
			GIM.GetItem(308).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(308) as IItem, (int)Config["Drills"], null);
			GIM.GetItem(310).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(310) as IItem, (int)Config["Sign"], null);
			GIM.GetItem(314).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(314) as IItem, (int)Config["Wrench"], null);
			GIM.GetItem(322).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(322) as IItem, (int)Config["Diamonds"], null);
			GIM.GetItem(323).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(323) as IItem, (int)Config["Diamonds"], null);
			GIM.GetItem(327).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(327) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(328).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(328) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(329).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(329) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(330).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(330) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(331).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(331) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(332).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(332) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(333).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(333) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(334).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(334) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(335).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(335) as IItem, (int)Config["KangaParts"], null);
			GIM.GetItem(336).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(336) as IItem, (int)Config["KangaPanels"], null);
			GIM.GetItem(337).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(337) as IItem, (int)Config["KangaPanels"], null);
			GIM.GetItem(338).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(338) as IItem, (int)Config["KangaPanels"], null);
			GIM.GetItem(339).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(339) as IItem, (int)Config["KangaPanels"], null);
			GIM.GetItem(340).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(340) as IItem, (int)Config["KangaPanels"], null);
			GIM.GetItem(341).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(341) as IItem, (int)Config["KangaPanels"], null);
			GIM.GetItem(342).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(342) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(343).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(343) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(344).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(344) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(345).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(345) as IItem, (int)Config["Designs"], null);
			GIM.GetItem(346).GetType().BaseType.GetProperty("MaxStackSize").SetValue(GIM.GetItem(346) as IItem, (int)Config["Designs"], null);
			
		}
		protected override void LoadDefaultConfig()
        {
			if(Config["Steak"] == null) Config.Set("Steak", 1);
			if(Config["Ruby"] == null) Config.Set("Ruby", 1);
			if(Config["Arrows"] == null) Config.Set("Arrows", 50);
			if(Config["FreshOwrong"] == null) Config.Set("FreshOwrong", 1);
			if(Config["Dynamite"] == null) Config.Set("Dynamite", 5);
			if(Config["C4"] == null) Config.Set("C4", 1);
			if(Config["Paints"] == null) Config.Set("Paints", 1);
			if(Config["PoisonTrap"] == null) Config.Set("PoisonTrap", 1);
			if(Config["CarParts"] == null) Config.Set("CarParts", 1);
			if(Config["CarPanels"] == null) Config.Set("CarPanels", 1);
			if(Config["GoatPanels"] == null) Config.Set("GoatPanels", 1);
			if(Config["Wheels"] == null) Config.Set("Wheels", 1);
			if(Config["Designs"] == null) Config.Set("Designs", 1);
			if(Config["Drills"] == null) Config.Set("Drills", 1);
			if(Config["Wrench"] == null) Config.Set("Wrench", 1);
			if(Config["OwnershipStake"] == null) Config.Set("OwnershipStake", 1);
			if(Config["ConstructionHammer"] == null) Config.Set("ConstructionHammer", 1);
			if(Config["BlastFurnace"] == null) Config.Set("BlastFurnace", 1);
			if(Config["Backpacks"] == null) Config.Set("Backpacks", 1);
			if(Config["LandcrabMine"] == null) Config.Set("LandcrabMine", 1);
			if(Config["Sign"] == null) Config.Set("Sign", 1);
			if(Config["Spears"] == null) Config.Set("Spears", 1);
			if(Config["Diamonds"] == null) Config.Set("Diamonds", 1);
			if(Config["KangaParts"] == null) Config.Set("KangaParts", 1);
			if(Config["KangaPanels"] == null) Config.Set("KangaPanels", 1);
            SaveConfig();
        }
	}
}