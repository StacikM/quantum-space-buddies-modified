using Mirror;
using QSB.HUD;
using QSB.Localization;
using QSB.Menus;
using QSB.Messaging;
using QSB.Utility;
using UnityEngine;

namespace QSB.Player.Messages
{
	public class PlayerKickMessage : QSBMessage<string>
	{
		private uint PlayerId;

		public PlayerKickMessage(uint playerId, string reason) : base(reason) =>
			PlayerId = playerId;

		public override void Serialize(NetworkWriter writer)
		{
			base.Serialize(writer);
			writer.Write(PlayerId);
		}

		public override void Deserialize(NetworkReader reader)
		{
			base.Deserialize(reader);
			PlayerId = reader.Read<uint>();
		}

		public override void OnReceiveRemote()
		{
			if (PlayerId != QSBPlayerManager.LocalPlayerId)
			{
				if (QSBPlayerManager.PlayerExists(PlayerId))
				{
					var name = QSBPlayerManager.GetPlayer(PlayerId).Name;
					MultiplayerHUDManager.Instance.WriteSystemMessage(
						$"<color=red>{name} was kicked from the server.</color>\n<color=grey>Reason:</color> {Data}",
						Color.red);
					return;
				}

				MultiplayerHUDManager.Instance.WriteSystemMessage($"Unknown player ID {PlayerId} was kicked.", Color.red);
				return;
			}

			MultiplayerHUDManager.Instance.WriteSystemMessage(
				$"<color=red>You were kicked from the server.</color>\n<color=grey>Reason:</color> {Data}",
				Color.red);
			MenuManager.Instance.OnKicked(Data);

			NetworkClient.Disconnect();
		}
	}
}
