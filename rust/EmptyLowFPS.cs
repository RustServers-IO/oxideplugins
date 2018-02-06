namespace Oxide.Plugins
{
    [Info("EmptyLowFPS", "Dezito", "0.0.4", ResourceId = 1889)]
    [Description("Set low cpu usage when no players connected")]

    class EmptyLowFPS : RustPlugin
    {
        private static bool UseFPSLimit = true;
        private static int MaxFPSLimit = 256;
        private static int EmptyFPSLimit = 30;

        private static bool UseTickrate = true;
        private static int MaxTickrate = 30;
        private static int EmptyTickrate = 10;

        void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<bool>("UseFPSLimit", ref UseFPSLimit);
            CheckCfg<int>("MaxFPSLimit", ref MaxFPSLimit);
            CheckCfg<int>("EmptyFPSLimit", ref EmptyFPSLimit);

            CheckCfg<bool>("UseTickrate", ref UseTickrate);
            CheckCfg<int>("MaxTickrate", ref MaxTickrate);
            CheckCfg<int>("EmptyTickrate", ref EmptyTickrate);

            SaveConfig();


            if (BasePlayer.activePlayerList.Count == 0)
            {
                if (UseFPSLimit)
                    ServerEmpty_FPSLimit();
                if (UseTickrate)
                    ServerEmpty_Tickrate();
            }
            else
            {
                if (UseFPSLimit)
                    ServerNotEmpty_FPSLimit();
                if (UseTickrate)
                    ServerNotEmpty_Tickrate();
            }
        }

        void OnPlayerConnected(Network.Message packet)
        {
            if (BasePlayer.activePlayerList.Count == 0)
            {
                if (UseFPSLimit)
                    ServerNotEmpty_FPSLimit();
                if (UseTickrate)
                ServerNotEmpty_Tickrate();
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (BasePlayer.activePlayerList.Count == 1)
            {
                if (UseFPSLimit)
                    ServerEmpty_FPSLimit();
                if (UseTickrate)
                    ServerEmpty_Tickrate();
            }
        }

        void Unload()
        {
            if (MaxFPSLimit != null)
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "fps.limit "+MaxFPSLimit);
                return;
            if (MaxTickrate != null)
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.tickrate "+MaxTickrate);
                return;
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "fps.limit 256");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.tickrate 30");
        }

        void ServerNotEmpty_FPSLimit()
        {
            Puts("Server is NOT empty setting fps.limit to " + MaxFPSLimit);
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "fps.limit "+ MaxFPSLimit);
        }

        void ServerEmpty_FPSLimit()
        {
            Puts("Server is empty setting fps.limit to " + EmptyFPSLimit);
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "fps.limit " + EmptyFPSLimit);
        }

        void ServerNotEmpty_Tickrate()
        {
            Puts("Server is NOT empty setting server.tickrate to " + MaxTickrate);
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.tickrate "+ MaxTickrate);
        }

        void ServerEmpty_Tickrate()
        {
            Puts("Server is empty setting server.tickrate to " + EmptyTickrate);
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.tickrate " + EmptyTickrate);
        }
    }
}
