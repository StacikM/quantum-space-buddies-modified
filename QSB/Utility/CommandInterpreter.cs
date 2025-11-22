using Mirror;
using OWML.Common;
using QSB.HUD;
using QSB.Messaging;
using QSB.Player;
using QSB.Player.Messages;
using QSB.RespawnSync;
using QSB.RespawnSync.Messages;
using QSB.Utility;
using QSB.WorldSync;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QSB.Utility;

public static class BanManager
{
	private static readonly HashSet<uint> bannedPlayers = new();

	public static bool IsBanned(uint playerId) => bannedPlayers.Contains(playerId);
	public static void Ban(uint playerId) => bannedPlayers.Add(playerId);
	public static void Unban(uint playerId) => bannedPlayers.Remove(playerId);
	public static void Clear() => bannedPlayers.Clear();
}

public static class ServerFreezeManager
{
	public static bool IsFrozen { get; private set; } = false;
	public static bool CanJoin(uint playerId) => !IsFrozen;
	public static void Freeze() => IsFrozen = true;
	public static void Unfreeze() => IsFrozen = false;
}

public class CommandInterpreter : MonoBehaviour, IAddComponentOnStart
{

	public static bool InterpretCommand(string message)
	{
		if (string.IsNullOrEmpty(message) || message[0] != '/')
			return false;

		var commandParts = message.Substring(1).Split(' ');
		var command = commandParts[0].ToLowerInvariant();

		switch (command)
		{
			// player cmds
			case "kick": KickPlayer(commandParts.Skip(1).ToArray()); break;
			case "ban": BanPlayer(commandParts.Skip(1).ToArray()); break;
			case "unban": UnbanPlayer(commandParts.Skip(1).ToArray()); break;
			case "mute": MutePlayer(commandParts.Skip(1).ToArray()); break;
			case "unmute": UnmutePlayer(commandParts.Skip(1).ToArray()); break;
			case "mutelist": ShowMutedPlayers(); break;

			// server ctrl
			case "serverfreeze": FreezeServer(); break;
			case "serverlock": FreezeServer(); break;
			case "unserverfreeze": UnfreezeServer(); break;
			case "unserverlock": UnfreezeServer(); break;

			// utility and fun
			case "revive": RevivePlayer(commandParts.Skip(1).ToArray()); break;
			case "kill": KillPlayer(commandParts.Skip(1).ToArray()); break;
			// the 2 above for some reasons arent synced with the clients

			// ship commands, some work some not
			case "ship": ShipCommand(commandParts.Skip(1).ToArray()); break;
			// general
			case "copy-id": CopySteamID(); break;

			// only really for web but /clearchat could be useful when chats bugging (chat bugs fixed later)
			case "say": SayCommand(commandParts.Skip(1).ToArray()); break;
			case "clearchat": ClearChatCommand(); break;


			default:
				WriteToChat($"Unknown command \"{command}\".", Color.red);
				break;
		}

		return true;
	}

	private static void WriteToChat(string message, Color color)
	{
		MultiplayerHUDManager.Instance.WriteMessage(message, color);
	}

	#region Chat stuff
	private static void SayCommand(string[] args)
	{
		if (args.Length == 0)
		{
			WriteToChat("Usage: /say <message>", Color.yellow);
			return;
		}

		string message = string.Join(" ", args);
		string formatted = $"{message}";

		// send to everyone
		foreach (var player in QSBPlayerManager.PlayerList)
		{
			MultiplayerHUDManager.Instance.WriteSystemMessage(formatted, Color.cyan);
		}

	}

	private static void ClearChatCommand()
	{
		MultiplayerHUDManager.Instance.ClearAllChatMessages();
		WriteToChat("Chat cleared for all players.", Color.yellow);
		DebugLog.ToConsole("[WebAdmin] Chat cleared.", MessageType.Info);
	}
	#endregion

	#region Player Management

	private static void KickPlayer(string[] args)
	{
		if (args.Length == 0) return;

		// get player by name
		var name = args[0];
		// get reason if provided
		var reason = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "No reason provided";
		// get the final player object
		var player = QSBPlayerManager.PlayerList.FirstOrDefault(p => p.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));

		// if player not found
		if (player == null)
		{
			WriteToChat($"Player {name} not found.", Color.red);
			return;
		}

		// kick the player
		new PlayerKickMessage(player.PlayerId, $"WARNING: Removed by host: {reason}").Send();
		// notify in chat
		WriteToChat($"{player.Name} was kicked.\nReason: {reason}", Color.yellow); // can for some reason break chat a little, if happens /clearchat
	}

	private static void BanPlayer(string[] args)
	{
		if (args.Length == 0) return;

		var name = args[0];
		var reason = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "No reason provided";
		var player = QSBPlayerManager.PlayerList.FirstOrDefault(p => p.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));

		if (player == null)
		{
			WriteToChat($"Player {name} not found.", Color.red);
			return;
		}


		// this adds the player to the ban list
		BanManager.Ban(player.PlayerId);
		// disconnects the player
		new PlayerKickMessage(player.PlayerId, $"WARNING: Server banned: {reason}").Send();
		WriteToChat($"{player.Name} has been banned.\nReason: {reason}", Color.red);
	}

	private static void UnbanPlayer(string[] args)
	{
		if (args.Length == 0) return;

		var name = args[0];
		var player = QSBPlayerManager.PlayerList.FirstOrDefault(p => p.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
		if (player == null)
		{
			WriteToChat($"Player {name} not found.", Color.red);
			return;
		}


		// removes from the list
		BanManager.Unban(player.PlayerId);
		WriteToChat($"{player.Name} has been unbanned.", Color.green);
	}

	private static void MutePlayer(string[] args)
	{
		if (args.Length == 0)
		{
			WriteToChat("Usage: /mute <playerName> [reason]", Color.yellow);
			return;
		}

		var name = args[0];
		var reason = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "No reason provided";
		var player = QSBPlayerManager.PlayerList.FirstOrDefault(p => p.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));

		if (player == null)
		{
			WriteToChat($"Player {name} not found.", Color.red);
			return;
		}

		MuteManager.Mute(player.PlayerId, reason);
		MultiplayerHUDManager.Instance.WriteSystemMessage(
			$"{player.Name} has been muted by the host.\nReason: {reason}",
			Color.yellow
		);
	}

	private static void UnmutePlayer(string[] args)
	{
		if (args.Length == 0)
		{
			WriteToChat("Usage: /unmute <playerName>", Color.yellow);
			return;
		}

		var name = args[0];
		var player = QSBPlayerManager.PlayerList.FirstOrDefault(p => p.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));

		if (player == null)
		{
			WriteToChat($"Player {name} not found.", Color.red);
			return;
		}

		if (!MuteManager.IsMuted(player.PlayerId))
		{
			WriteToChat($"{player.Name} is not muted.", Color.red);
			return;
		}

		MuteManager.Unmute(player.PlayerId);
		MultiplayerHUDManager.Instance.WriteSystemMessage(
			$"{player.Name} has been unmuted by the host.",
			Color.green
		);
	}

	private static void ShowMutedPlayers()
	{
		var muted = QSBPlayerManager.PlayerList
			.Where(p => MuteManager.IsMuted(p.PlayerId))
			.Select(p => $"{p.Name} ({MuteManager.GetReason(p.PlayerId)})")
			.ToList();

		if (muted.Count == 0)
			WriteToChat("No players are currently muted. Lucky for them", Color.green);
		else
			WriteToChat("Muted players:\n" + string.Join("\n", muted), Color.yellow);
	}

	#endregion

	#region Server Control

	// this is a bit broken, sometimes players can still join sometimes not
	// one time my friend was 454395734875349 seconds behind the server when i used it and he rejoined, don't know if its QSB or the server freeze
	private static void FreezeServer()
	{
		ServerFreezeManager.Freeze();

		WriteToChat("Server is now frozen. Players cannot join until /unserverfreeze is used.", Color.yellow);
	}

	private static void UnfreezeServer()
	{
		ServerFreezeManager.Unfreeze();
		WriteToChat("Server is now unfrozen. Players may join again.", Color.green);
	}

	#endregion

	#region Utility Commands

	private static void KillPlayer(string[] args)
	{
		if (args.Length == 0) return;
		var player = QSBPlayerManager.PlayerList.FirstOrDefault(p => p.Name.Equals(args[0], System.StringComparison.OrdinalIgnoreCase));
		if (player == null) { WriteToChat($"Player {args[0]} not found.", Color.red); return; }

		WriteToChat($"Killed {player.Name}", Color.yellow);
		new QSB.DeathSync.Messages.PlayerDeathMessage(DeathType.Default).Send();
	}

	private static void RevivePlayer(string[] args)
	{
		if (args.Length == 0) return;
		var player = QSBPlayerManager.PlayerList.FirstOrDefault(p => p.Name.Equals(args[0], System.StringComparison.OrdinalIgnoreCase));
		if (player == null) { WriteToChat($"Player {args[0]} not found.", Color.red); return; }

		new PlayerRespawnMessage(player.PlayerId).Send();
		WriteToChat($"Revived {player.Name}", Color.green);
	}

	private static void ShipCommand(string[] arguments)
	{
		if (arguments.Length == 0) return;
		var cmd = arguments[0];

		switch (cmd)
		{
			case "open-hatch":
				QSBWorldSync.GetUnityObject<HatchController>().OpenHatch();
				new QSB.ShipSync.Messages.HatchMessage(true).Send();
				WriteToChat("opened ship hatch.", Color.green);
				break;

			case "close-hatch":
				QSBWorldSync.GetUnityObject<HatchController>().CloseHatch();
				new QSB.ShipSync.Messages.HatchMessage(false).Send();
				WriteToChat("closed ship hatch.", Color.green);
				WriteToChat("WARNING: this can cause that the player cannot open the hatch again unless using /ship open-hatch", Color.yellow);
				break;

			default:
				WriteToChat($"Unknown ship command \"{cmd}\".", Color.red);
				break;
		}
	}

	private static void CopySteamID()
	{
		var id = QSBCore.IsHost ? SteamUser.GetSteamID().ToString() : QSBNetworkManager.singleton.networkAddress;
		GUIUtility.systemCopyBuffer = id;
		WriteToChat($"Copied {id} to clipboard.", Color.green);
	}

	#endregion

	private void Awake()
	{
		// Auto-kick banned or if server is in maintenance on join
		QSBPlayerManager.OnAddPlayer += player =>
		{
			if (!QSBCore.IsHost) return;

			if (BanManager.IsBanned(player.PlayerId))
			{
				new PlayerKickMessage(player.PlayerId, "You are currently banned from this server. Try again later").Send();
				DebugLog.ToConsole($"[Moderation] auto-kicked banned player {player.Name}");
				return;
			}

			if (ServerFreezeManager.IsFrozen)
			{
				new PlayerKickMessage(player.PlayerId, "Server is whitelisted. Please ask the host to write /unserverfreeze").Send();
				DebugLog.ToConsole($"[moderation] auto-kicked {player.Name} (server frozen)");
			}
		};


		// Clear bans and mutes on host leave
		QSBPlayerManager.OnRemovePlayer += player =>
		{
			if (player.IsLocalPlayer)
			{
				BanManager.Clear(); // reset bans
				MuteManager.Reset(); // clear mutes
			}
		};
	}
}
