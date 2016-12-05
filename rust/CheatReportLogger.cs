using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info( "CheatReportLogger", "mvrb", "1.0.0" )]
	[Description( "Saves cheat reports from the F7 menu to a file and/or RCON." )]
    class CheatReportLogger : RustPlugin
    {
		bool logToFile;
		bool logToRCON;
		
		protected override void LoadDefaultConfig()
        {
            Config["LogToFile"] = logToFile = GetConfig( "LogToFile", true );
            Config["LogToRCON"] = logToRCON = GetConfig( "LogToRCON", true );

            SaveConfig();
        }
        
        void Init()
		{
			LoadDefaultConfig();
			LoadDefaultMessages();
		}
		
		void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages( new Dictionary<string, string>
            {
                ["RCON"] = "{0} [{1}] {2} used F7 to report {3}",
                ["File"] = "[{0}] {1} used F7 to report {2}"
            }, this );
        }
		
		void OnServerCommand( ConsoleSystem.Arg arg )
        {
			if ( arg.connection == null ) return;

            var command = arg.cmd.namefull;
            var args = arg.GetString( 0, "text" );
			
            if ( command == "server.cheatreport" ) 
			{				
				if( logToRCON )
					Puts( Lang( "RCON", null, DateTime.Now.ToString( "yyyy-dd-MM H:mm:ss" ), arg.connection.userid, arg.connection.username, arg.ArgsStr ) );
				if( logToFile )
					ConVar.Server.Log( "Oxide/Logs/CheatReportLogger.txt", Lang( "File", null, arg.connection.userid, arg.connection.username, arg.ArgsStr ) );
			}
        }
		
		T GetConfig<T>( string name, T value ) => Config[name] == null ? value : ( T )Convert.ChangeType( Config[name], typeof( T ) );
		
		string Lang( string key, string id = null, params object[] args ) => string.Format( lang.GetMessage( key, this, id ), args );
	}
}