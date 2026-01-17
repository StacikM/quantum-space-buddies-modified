using QSB.Player;
using QSB.Player.Messages;
using System.Collections.Generic;
using QSB.Messaging;
using QSB.Utility;

namespace QSB.Utility;

public static class BackendAuthManager
{
	public static bool Enabled { get; set; } = false;

	// players that successfully authenticated with backend
	private static readonly HashSet<uint> authenticatedPlayers = new();

	public static void Enable() => Enabled = true;
	public static void Disable()
	{
		Enabled = false;
		authenticatedPlayers.Clear();
	}

	public static void MarkAuthenticated(uint playerId)
	{
		authenticatedPlayers.Add(playerId);
	}

	public static bool IsAuthenticated(uint playerId)
	{
		return authenticatedPlayers.Contains(playerId);
	}

	public static void OnPlayerLeft(uint playerId)
	{
		authenticatedPlayers.Remove(playerId);
	}
}

public class BackendAuthConfirmMessage : QSBMessage
{
	public uint PlayerId;

	public BackendAuthConfirmMessage(uint playerId)
	{
		PlayerId = playerId;
	}

	public override void OnReceiveRemote()
	{
		if (!QSBCore.IsHost) return;

		BackendAuthManager.MarkAuthenticated(PlayerId);
	}
}