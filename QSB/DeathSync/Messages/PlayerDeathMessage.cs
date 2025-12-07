using Mirror;
using QSB.ClientServerStateSync;
using QSB.HUD;
using QSB.Messaging;
using QSB.Player;
using QSB.RespawnSync;
using QSB.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using QSB.DeathSync;


namespace QSB.DeathSync.Messages;

public class PlayerDeathMessage : QSBMessage<DeathType>
{
	private int NecronomiconIndex;
	public uint TargetId;

	public PlayerDeathMessage(uint targetId, DeathType type) : base(type)
	{
		TargetId = targetId;
		NecronomiconIndex = Necronomicon.GetRandomIndex(type);
	}
	public override void Serialize(NetworkWriter writer)
	{
		base.Serialize(writer);
		writer.Write(NecronomiconIndex);
		writer.Write(TargetId);
	}

	public override void Deserialize(NetworkReader reader)
	{
		base.Deserialize(reader);
		NecronomiconIndex = reader.Read<int>();
		TargetId = reader.Read<uint>();
	}

	public override void OnReceiveLocal()
	{
		var player = QSBPlayerManager.GetPlayer(TargetId);
		RespawnManager.Instance.OnPlayerDeath(player);
		ClientStateManager.Instance.OnDeath();
	}

	public override void OnReceiveRemote()
	{
		var player = QSBPlayerManager.GetPlayer(TargetId);
		var playerName = player.Name;
		var deathMessage = Necronomicon.GetPhrase(Data, NecronomiconIndex);
		if (deathMessage != null)
		{
			MultiplayerHUDManager.Instance.WriteSystemMessage(string.Format(deathMessage, playerName), Color.grey);
		}

		RespawnManager.Instance.OnPlayerDeath(player);
	}
}