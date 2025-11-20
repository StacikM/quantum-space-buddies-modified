using QSB.HUD.Messages;
using QSB.Messaging;
using QSB.Player;
using QSB.Utility;
using UnityEngine;

namespace QSB.Moderation
{
	public static class ServerModerationFilter

	//returns true if the chat message is allowed, false if it should be blocked
	{
		public static bool ValidateChat(uint senderId, string message)
		{
			if (!QSBCore.IsHost)
				return true;

			// prevent banned users from sending or appearing active
			if (BanManager.IsBanned(senderId))
			{
				DebugLog.ToConsole($"[Moderation] Blocked message from banned player {senderId}");
				return false;
			}

			// block chat from muted users
			if (MuteManager.IsMuted(senderId))
			{
				DebugLog.ToConsole($"[Moderation] Blocked chat from muted player {senderId}");

				return false;
			}

			// Allowed
			return true;
		}
	}
}
