using System.Collections.Generic;

namespace QSB.Utility
{
	// this is basically a storage for muted players
	// the muting logic is handled in CommandInterpreter, but the check for being muted is in MultiplayerHUD and also super advanced protection in ServerModerationFilter
	public static class MuteManager
	{
		private static readonly Dictionary<uint, string> _mutedPlayers = new();

		public static bool IsMuted(uint playerId) => _mutedPlayers.ContainsKey(playerId);

		public static void Mute(uint playerId, string reason = "No reason provided.")
		{
			_mutedPlayers[playerId] = reason;
		}

		public static void Unmute(uint playerId)
		{
			_mutedPlayers.Remove(playerId);
		}

		public static string GetReason(uint playerId)
		{
			return _mutedPlayers.TryGetValue(playerId, out var reason) ? reason : "No reason provided.";
		}

		public static void Reset()
		{
			_mutedPlayers.Clear();
		}
	}
}
