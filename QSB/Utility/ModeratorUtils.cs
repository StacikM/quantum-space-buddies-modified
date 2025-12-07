using Mirror;
using QSB.Messaging;
using QSB.Player;
using QSB.Player.Messages;
using QSB.Utility;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace QSB.Moderation
{
	public static class ModerationUtils
	{
		// first tries to kick gracefully, then forcefully disconnects after a short delay if still present
		public static void ForceDesyncPlayer(PlayerInfo player, string reason = "You have been removed by the host.")
		{
			if (player == null)
				return;

			try
			{
				DebugLog.ToConsole($"[moderation] forcing desync for player {player.Name} ({player.PlayerId}) - reason: {reason}", OWML.Common.MessageType.Info);

				// 1) try nice sunshine and rainbows kick first (this will show the kick UI client-side)
				try
				{
					new PlayerKickMessage(player.PlayerId, reason).Send();
				}
				catch (Exception ex)
				{
					DebugLog.ToConsole($"[moderation] PlayerKickMessage.Send() failed: {ex.Message} (a bypass mod)", OWML.Common.MessageType.Warning);
				}

				// 2) schedule a forceful disconnect after a short delay if they're still present
				Delay.RunFramesLater((int)24.0f, () =>
				{
					// if player was removed by graceful path, don't proceed
					var stillExists = QSBPlayerManager.PlayerExists(player.PlayerId);
					if (!stillExists)
					{
						DebugLog.ToConsole($"[moderation] player {player.Name} already removed by graceful kick.", OWML.Common.MessageType.Info);
						return;
					}

					// locate the NetworkConnection for this player via multiple strategies
					NetworkConnection conn = FindConnectionForPlayer(player);

					if (conn != null)
					{
						try
						{
							DebugLog.ToConsole($"[moderation] forcefully disconnecting connection {conn.connectionId} for player {player.Name}", OWML.Common.MessageType.Info);

							// try to destroy player objects if any
							try
							{
								NetworkServer.DestroyPlayerForConnection((NetworkConnectionToClient)conn);
							}
							catch { }

							// finally disconnect
							try
							{
								// desyncs the player
								conn.Disconnect();
							}
							catch
							{
								try
								{
									// fallback
									NetworkServer.DestroyPlayerForConnection((NetworkConnectionToClient)conn);
									NetworkServer.connections[conn.connectionId]?.Disconnect();
								}
								catch (Exception inner)
								{
									DebugLog.ToConsole($"[Moderation] force disconnect fallback failed: {inner.Message}", OWML.Common.MessageType.Error);
								}
							}
						}
						catch (Exception ex)
						{
							DebugLog.ToConsole($"[Moderation] failed to disconnect player {player.Name}: {ex.Message}", OWML.Common.MessageType.Error);
						}
					}
					else
					{
						DebugLog.ToConsole($"[moderation] Could not find NetworkConnection for player {player.Name}. Attempting to remove player entry.", OWML.Common.MessageType.Warning);

						// try to remove player entry directly
						try
						{
							var removeEv = typeof(QSBPlayerManager).GetEvent("OnRemovePlayer", BindingFlags.Public | BindingFlags.Static);
							// can't reliably remove, so just warn
							DebugLog.ToConsole($"[moderation] No direct connection found - player entry may remain but will be unsynced.", OWML.Common.MessageType.Warning);
						}
						catch { /* ignore */ }
					}
				});
			}
			catch (Exception e)
			{
				DebugLog.ToConsole($"[moderation] exception in ForceDesyncPlayer: {e}", OWML.Common.MessageType.Error);
			}
		}

		// tries to find the NetworkConnection for a given PlayerInfo via multiple strategies
		//this part was made with the help of ChatGPT because it kept failing to find the connection with original code, but this one seems to work better
		private static NetworkConnection FindConnectionForPlayer(PlayerInfo player)
		{
			if (player == null) return null;

			// Strategy A: PlayerInfo may expose a ConnectionToClient property (Mirror style)
			try
			{
				var prop = player.GetType().GetProperty("ConnectionToClient", BindingFlags.Public | BindingFlags.Instance);
				if (prop != null)
				{
					var value = prop.GetValue(player);
					if (value is NetworkConnection nc)
						return nc;
				}
			}
			catch { }

			// Strategy B: PlayerInfo may store a network identity/netId we can use to match connection.identity
			try
			{
				// common field names that might hold identity/netId - check dynamically
				var netIdField = player.GetType().GetField("netId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
								 ?? player.GetType().GetField("NetworkId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

				if (netIdField != null)
				{
					var netIdVal = netIdField.GetValue(player);
					if (netIdVal != null && ulong.TryParse(netIdVal.ToString(), out var parsed))
					{
						foreach (var kv in NetworkServer.connections)
						{
							var c = kv.Value;
							if (c == null) continue;
							if (c.identity != null && c.identity.netId.ToString() == parsed.ToString())
								return c;
						}
					}
				}
			}
			catch { }

			// Strategy C: iterate NetworkServer.connections and match by player name through spawned identity (best-effort)
			try
			{
				foreach (var kv in NetworkServer.connections)
				{
					var c = kv.Value;
					if (c == null) continue;

					// try find a NetworkIdentity with a PlayerInfo/PlayerName component in this connection's identity
					var identity = c.identity;
					if (identity == null) continue;

					// check if the identity's GameObject holds a QSB PlayerInfo reference (rare)
					var comps = identity.gameObject.GetComponentsInChildren<MonoBehaviour>(true);
					foreach (var comp in comps)
					{
						try
						{
							var t = comp.GetType();
							if (t.Name.Contains("Player") || t.Name.Contains("PlayerInfo"))
							{
								var nameProp = t.GetProperty("Name");
								if (nameProp != null)
								{
									var nm = nameProp.GetValue(comp) as string;
									if (!string.IsNullOrEmpty(nm) && nm.Equals(player.Name, StringComparison.OrdinalIgnoreCase))
										return c;
								}
							}
						}
						catch { }
					}
				}
			}
			catch { }

			// Strategy D: last resort - match by connection.address to player display (not reliable)
			try
			{
				foreach (var kv in NetworkServer.connections)
				{
					var c = kv.Value;
					if (c == null) continue;
					if (c.address != null && c.address.Contains(player.Name))
						return c;
				}
			}
			catch { }

			return null;
		}
	}
}
