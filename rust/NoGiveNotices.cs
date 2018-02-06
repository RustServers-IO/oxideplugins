namespace Oxide.Plugins
{
    [Info("NoGiveNotices", "Wulf/lukespragg", "0.1.0", ResourceId = 2336)]
    [Description("Prevents admin item giving notices from showing in the chat")]

    class NoGiveNotices : RustPlugin
    {
        object OnServerMessage(string m, string n) => m.Contains("gave") && n == "SERVER" ? (object)true : null;
    }
}
