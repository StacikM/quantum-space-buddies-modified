using QSB.ClientServerStateSync;
using QSB.DeathSync.Messages;
using QSB.HUD;
using QSB.Messaging;
using QSB.Player;
using QSB.Utility.Messages;
using QSB.WorldSync;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QSB.Utility;

public class RestartLoopVoteManager : MonoBehaviour, IAddComponentOnStart
{
	public static RestartLoopVoteManager Instance { get; private set; }

	private readonly HashSet<uint> _voters = new();
	private readonly Dictionary<uint, bool> _ballots = new();
	private float _deadline;
	private bool _active;

	private static bool CanRestart => QSBCore.IsInMultiplayer
		&& QSBSceneManager.CurrentScene == OWScene.SolarSystem
		&& QSBWorldSync.AllObjectsReady
		&& ServerStateManager.Instance != null
		&& ServerStateManager.Instance.GetServerState() == ServerState.InSolarSystem;

	public static bool TryInterpretCommand(string message)
	{
		var parts = message.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
		{
			return false;
		}

		var command = parts[0].ToLowerInvariant();
		if (command != "vote" && command != "yes" && command != "no")
		{
			return false;
		}

		if ((command == "vote" && (parts.Length != 2 || !parts[1].Equals("restartLoop", StringComparison.OrdinalIgnoreCase)))
			|| (command != "vote" && parts.Length != 1))
		{
			MultiplayerHUDManager.Instance.WriteSystemMessage(
				command == "vote" ? "Usage: /vote restartLoop" : $"Usage: /{command}", Color.yellow);
			return true;
		}

		if (!CanRestart)
		{
			MultiplayerHUDManager.Instance.WriteSystemMessage("Loop voting is only available during an active multiplayer loop.", Color.yellow);
			return true;
		}

		new RestartLoopVoteRequestMessage(command).Send();
		return true;
	}

	public void HandleRequest(uint playerId, string command)
	{
		if (!QSBCore.IsHost || !CanRestart)
		{
			return;
		}

		var player = QSBPlayerManager.PlayerList.FirstOrDefault(x => x.PlayerId == playerId && x.IsReady);
		if (player == null || BanManager.IsBanned(player.SteamId))
		{
			return;
		}

		UpdateVote();
		if (command == "vote")
		{
			if (_active)
			{
				Notify("A restart vote is already active. Use /yes or /no.", playerId);
				return;
			}

			_voters.UnionWith(QSBPlayerManager.PlayerList.Where(x => x.IsReady).Select(x => x.PlayerId));
			_active = true;
			_deadline = Time.realtimeSinceStartup + 30f;
			Notify($"{player.Name} started a loop restart vote. Type /yes or /no within 30 seconds. {_voters.Count / 2 + 1} yes votes needed.");
			return;
		}

		if (command != "yes" && command != "no")
		{
			return;
		}

		if (!_active)
		{
			Notify("No loop restart vote is active. Start one with /vote restartLoop.", playerId);
			return;
		}

		if (!_voters.Contains(playerId))
		{
			Notify("You joined after this vote started. You can vote in the next one.", playerId);
			return;
		}

		if (_ballots.ContainsKey(playerId))
		{
			Notify("You have already voted.", playerId);
			return;
		}

		_ballots.Add(playerId, command == "yes");
		Notify($"Vote recorded: {command}.", playerId);
		EvaluateVote();
	}

	private static void Notify(string message, uint to = uint.MaxValue)
		=> new RestartLoopVoteNoticeMessage(message) { To = to }.Send();

	private void Awake()
	{
		Instance = this;
		QSBSceneManager.OnPreSceneLoad += OnPreSceneLoad;
	}

	private void OnDestroy()
	{
		QSBSceneManager.OnPreSceneLoad -= OnPreSceneLoad;
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void OnPreSceneLoad(OWScene oldScene, OWScene newScene) => ClearVote();

	private void Update() => UpdateVote();

	private void UpdateVote()
	{
		if (!_active)
		{
			return;
		}

		if (!QSBCore.IsHost || !CanRestart)
		{
			ClearVote();
			return;
		}

		if (Time.realtimeSinceStartup >= _deadline)
		{
			ClearVote();
			Notify("Loop restart vote expired. The loop continues.");
			return;
		}

		_voters.RemoveWhere(id => !QSBPlayerManager.PlayerList.Any(x => x.PlayerId == id));
		foreach (var id in _ballots.Keys.Where(id => !_voters.Contains(id)).ToArray())
		{
			_ballots.Remove(id);
		}

		EvaluateVote();
	}

	private void EvaluateVote()
	{
		var required = _voters.Count / 2 + 1;
		var yes = _ballots.Values.Count(x => x);
		if (_voters.Count == 0)
		{
			ClearVote();
		}
		else if (yes >= required)
		{
			ClearVote();
			Notify("Vote passed. Restarting the loop now!");
			new EndLoopMessage().Send();
		}
		else if (yes + _voters.Count - _ballots.Count < required)
		{
			ClearVote();
			Notify("Loop restart vote failed. The loop continues.");
		}
	}

	private void ClearVote()
	{
		_active = false;
		_voters.Clear();
		_ballots.Clear();
	}
}
