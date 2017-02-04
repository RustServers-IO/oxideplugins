using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using ProtoBuf;
using Network;

namespace Oxide.Plugins
{
    [Info("BackupExt", "Fujikura", "0.3.1", ResourceId = 2137 )]
    class BackupExt : RustPlugin
    {
		bool Changed;
		bool _backup;
		int currentRetry;
		string [] backupFolders;
		string [] backupFoldersShutdown;

		int numberOfBackups;
		bool backupBroadcast;
		int backupDelay;
		bool useBroadcastDelay;
		string prefix;
		string prefixColor;
		bool useTimer;
		int timerInterval;
		int maxPlayers;
		int maxRetry;
		int delayRetrySeconds;
	
		object GetConfig(string menu, string datavalue, object defaultValue)
		{
			var data = Config[menu] as Dictionary<string, object>;
			if (data == null)
			{
				data = new Dictionary<string, object>();
				Config[menu] = data;
				Changed = true;
			}
			object value;
			if (!data.TryGetValue(datavalue, out value))
			{
				value = defaultValue;
				data[datavalue] = value;
				Changed = true;
			}
			return value;
		}

		void LoadVariables()
		{
			numberOfBackups = Convert.ToInt32(GetConfig("Settings", "numberOfBackups", 8));
			backupBroadcast = Convert.ToBoolean(GetConfig("Notification", "backupBroadcast", false));
			backupDelay = Convert.ToInt32(GetConfig("Notification", "backupDelay", 5));
			useBroadcastDelay = Convert.ToBoolean(GetConfig("Notification", "useBroadcastDelay", true));
			prefix = Convert.ToString(GetConfig("Notification", "prefix", "BACKUP"));
			prefixColor = Convert.ToString(GetConfig("Notification", "prefixColor", "orange"));
			useTimer = Convert.ToBoolean(GetConfig("Timer", "useTimer", false));
			timerInterval = Convert.ToInt32(GetConfig("Timer", "timerInterval", 3600));
			maxPlayers = Convert.ToInt32(GetConfig("Timer", "maxPlayers", 20));
			maxRetry =  Convert.ToInt32(GetConfig("Timer", "maxRetry", 10));
			delayRetrySeconds = Convert.ToInt32(GetConfig("Timer", "delayRetrySeconds", 120));

			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}
		
		void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			                      {
									{"backupfinish", "Backup process finished."},
									{"backupannounce", "Starting server backup in {0} seconds."},
									{"backuprunning", "Running server backup."},
									{"backupautomatic", "Running automated server backup every {0} seconds."},									
									{"backupdelay", "Backup delayed ({0} of {1}) for next '{2}' seconds."},				                      
								  },this);
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}
		
		void Loaded()
		{
			LoadVariables();
			LoadDefaultMessages();
			backupFolders = BackupFolders();
			backupFoldersShutdown = BackupFoldersShutdown();
		}

		void OnServerInitialized()
        {
			currentRetry = 0;
			if (useTimer)
			{
				timer.Once(timerInterval, TimerCheck);
				Puts(string.Format(lang.GetMessage("backupautomatic", this), timerInterval));
			}
        }
		
		void OnServerShutdown()
		{
			try { DirectoryEx.Backup(BackupFoldersShutdown()); } catch {}
		}		

		void BackupCreate(bool manual = false)
		{
			DirectoryEx.Backup(BackupFolders());
			DirectoryEx.CopyAll(ConVar.Server.rootFolder, backupFolders[0]);
			if (!manual)
				Puts(lang.GetMessage("backupfinish", this));
		}
		
		void TimerCheck()
		{
			if (SaveRestore.IsSaving)
			{
				timer.Once(1f, TimerCheck);
				return;
			}
			if (BasePlayer.activePlayerList.Count > maxPlayers && currentRetry < maxRetry)
			{
				currentRetry++;
				Puts(string.Format(lang.GetMessage("backupdelay", this), currentRetry, maxRetry, delayRetrySeconds));
				timer.Once(delayRetrySeconds, TimerCheck);
			}
			else
			{
				currentRetry = 0;
				ccmdExtBackup(new ConsoleSystem.Arg(null));
				timer.Once(timerInterval, TimerCheck);
			}
		}

		[ConsoleCommand("extbackup")]
		void ccmdExtBackup(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < 2) return;
			if (backupBroadcast)
			{
				if (useBroadcastDelay)
				{
					SendReply(arg, string.Format(lang.GetMessage("backupannounce", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ), backupDelay));
					BroadcastChat(string.Format(lang.GetMessage("backupannounce", this), backupDelay));
					timer.Once(backupDelay, () => BackupRun(arg));
				}
				else
				{
					timer.Once(0f, () => BackupRun(arg));
				}
			}
			else
				timer.Once(0f, () => BackupRun(arg));
		}
		
		void BackupRun(ConsoleSystem.Arg arg)
		{
			if (backupBroadcast)
				BroadcastChat(lang.GetMessage("backuprunning", this));
			SendReply(arg, lang.GetMessage("backuprunning", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
			BackupCreate(true);
			SendReply(arg, lang.GetMessage("backupfinish", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
			if (backupBroadcast)
				BroadcastChat(lang.GetMessage("backupfinish", this));
		}
		
		string [] BackupFolders()
		{
			string [] dp = new string[numberOfBackups];
			for (int i = 0; i < numberOfBackups; i++)
				dp[i] = $"backup/{i}/{ConVar.Server.identity}";
			return dp;
		}
		
		string [] BackupFoldersShutdown()
		{
			string [] dp = new string[numberOfBackups];
			for (int i = 3; i < numberOfBackups; i++)
				dp[i] = $"backup/{i}/{ConVar.Server.identity}";
			return dp;
		}
		
		void BroadcastChat(string msg = null) => PrintToChat(msg == null ? prefix : "<color=" + prefixColor + ">" + prefix + "</color>: " + msg);
	}
}