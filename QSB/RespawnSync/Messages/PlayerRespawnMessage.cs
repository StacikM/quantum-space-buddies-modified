using QSB.ClientServerStateSync;
using QSB.Messaging;
using QSB.Player;
using QSB.WorldSync;

namespace QSB.RespawnSync.Messages;

public class PlayerRespawnMessage : QSBMessage<uint>
{
	public PlayerRespawnMessage(uint playerId) : base(playerId) { }

	public override void OnReceiveLocal() => OnReceiveRemote();

	public override void OnReceiveRemote()
	{
		if (!QSBPlayerManager.PlayerExists(Data) || QSBSceneManager.CurrentScene != OWScene.SolarSystem
			|| !QSBWorldSync.AllObjectsReady
			|| ServerStateManager.Instance == null
			|| ServerStateManager.Instance.GetServerState() != ServerState.InSolarSystem)
		{
			return;
		}

		var player = QSBPlayerManager.GetPlayer(Data);
		if (!player.IsDead)
		{
			return;
		}

		if (Data == QSBPlayerManager.LocalPlayerId)
		{
			if (!RespawnManager.Instance.Respawn())
			{
				return;
			}
			ClientStateManager.Instance.OnRespawn();
		}

		RespawnManager.Instance.OnPlayerRespawn(player);
	}
}
