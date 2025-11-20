using QSB.Messaging;
using QSB.Player;
using QSB.Utility;
using UnityEngine;

namespace QSB.HUD.Messages
{
	public class ChatMessage : QSBMessage<(string message, Color color)>
	{
		public ChatMessage(string msg, Color color) : base((msg, color)) { }

		public override void OnReceiveLocal() => OnReceiveRemote();

		public override void OnReceiveRemote()
		{
			var fromPlayer = QSBPlayerManager.GetPlayer(From);

			if (QSBCore.IsHost && fromPlayer != null)
			{
				// Check ban
				if (BanManager.IsBanned(fromPlayer.PlayerId))
				{
					DebugLog.ToConsole($"[Moderation] Blocked chat from banned player {fromPlayer.Name}");
					return; // Drop silently
				}

				// Check mute
				if (MuteManager.IsMuted(fromPlayer.PlayerId))
				{
					DebugLog.ToConsole($"[Moderation] Blocked chat from muted player {fromPlayer.Name}");

					return; 
				}
			}

			MultiplayerHUDManager.Instance.WriteMessage(Data.message, Data.color);

			var qsb = false;
			string name;

			if (Data.message.StartsWith("QSB: "))
			{
				name = "QSB: ";
				qsb = true;
			}
			else if (fromPlayer != null && Data.message.StartsWith($"{fromPlayer.Name}: "))
			{
				name = $"{fromPlayer.Name}: ";
			}
			else
			{
				MultiplayerHUDManager.OnChatMessageEvent.Invoke(Data.message, From);
				return;
			}

			var messageWithoutName = Data.message.Remove(Data.message.IndexOf(name), name.Length);
			MultiplayerHUDManager.OnChatMessageEvent.Invoke(messageWithoutName, qsb ? uint.MaxValue : From);
		}
	}

	public class PlayerChatErrorMessage : QSBMessage<string>
	{
		public PlayerChatErrorMessage(string error) : base(error) { }

		public override void OnReceiveRemote()
		{
			MultiplayerHUDManager.Instance.WriteSystemMessage($"<color=red>{Data}</color>", Color.red);
		}
	}
}
