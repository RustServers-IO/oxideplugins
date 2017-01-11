using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("CustomDeathMessages", "Wil Simpson", "1.0.5")]
    [Description("Just displays custom death messages.")]

    class CustomDeathMessages : HurtworldPlugin
    {
        protected override void LoadDefaultConfig()
        {
            Config["TextColor"] = "#FFBF00";
            Config["ServerColor"] = "#80FF00";
            Config["ServerName"] = "Server";
            SaveConfig();
        }

        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"EntityStats/Sources/Fall Damage","{Name} fell to death"},
                {"EntityStats/Sources/Damage Over Time","{Name} just died"},
                {"EntityStats/Sources/Radiation Poisoning","{Name} just died"},
                {"EntityStats/Sources/Starvation","{Name} just died"},
                {"EntityStats/Sources/Hypothermia","{Name} just died"},
                {"EntityStats/Sources/Hyperthermia","{Name} just died"},
                {"EntityStats/Sources/Asphyxiation","{Name} just died"},
                {"EntityStats/Sources/Poison","{Name} just died"},
                {"EntityStats/Sources/Burning","{Name} just died"},
                {"EntityStats/Sources/Suicide","{Name} committed suicide"},
                {"EntityStats/Sources/Explosives","{Name} dead by explosion"},
                {"EntityStats/Sources/a Vehicle Impact","{Name} killed by a car"},
                {"Creatures/Tokar","{Name} got killed by Tokar"},
                {"Creatures/Shigi","{Name} got killed by a Shigi"},
                {"Creatures/Bor","{Name} got killed by a Bor"},
                {"Creatures/Radiation Bor","{Name} got killed by a Radiation Bor"},
                {"Creatures/Yeti","{Name} got killed by a Yeti"},
                {"Creatures/Sasquatch","{Name} got killed by a Sasquatch"},
                {"Machines/Medusa Vine", "{Name} killed by Medusa Trap"},
                {"Machines/Landmine", "{Name} killed by Landmine"},
                {"player","{Name} got killed by {Killer}"},
                {"Unknown","{Name} just died on a mystic way"}
            };

            lang.RegisterMessages(messages, this);
        }

        void Loaded() => LoadDefaultMessages();


        string GetNameOfObject(UnityEngine.GameObject obj)
        {
            var ManagerInstance = GameManager.Instance;
            return ManagerInstance.GetDescriptionKey(obj);
        }

        private void OnPlayerDeath(PlayerSession playerSession, EntityEffectSourceData dataSource)
        {
            var prefix = $"<color={Config["ServerColor"]}>{Config["ServerName"]}</color>";
            var textcolor = $"<color={Config["TextColor"]}>";
            string name = playerSession.Name;
            string KillerName = GetNameOfObject(dataSource.EntitySource);
            if (KillerName == "")
            {
                hurt.BroadcastChat(prefix, textcolor + (lang.GetMessage(dataSource.SourceDescriptionKey, this) ?? lang.GetMessage("Unknown", this)).Replace("{Name}", name) + "</color>");
            }
            else
            {
                hurt.BroadcastChat(prefix, textcolor + (lang.GetMessage(dataSource.SourceDescriptionKey, this) ?? lang.GetMessage("player", this)).Replace("{Name}", name).Replace("{Killer}", KillerName) + "</color>");
            }
        }
    }
}