using Newtonsoft.Json;
using OWML.Common;
using QSB.Messaging;
using QSB.Player;
using QSB.Utility;
using Steamworks;
using System.Net.Http;
using System.Text;
using UnityEngine;

public static class BackendAuthClient
{
	private static readonly HttpClient http = new();

	public static async void Authenticate()
	{
		if (!BackendAuthManager.Enabled)
			return;

		try
		{
			string ip;
			using (var ipClient = new HttpClient())
				ip = (await ipClient.GetStringAsync("https://api.ipify.org")).Trim();

			var payload = new
			{
				lobbyId = LobbyReporter.lobbyId,
				playerId = QSBPlayerManager.LocalPlayerId,
				playerName = QSBPlayerManager.LocalPlayer.Name,
				steamId = SteamUser.GetSteamID().ToString(),
				ip
			};

			var content = new StringContent(
				JsonConvert.SerializeObject(payload),
				Encoding.UTF8,
				"application/json"
			);

			var res = await http.PostAsync(
				"https://server.ctksystem.com/auth/join",
				content
			);

			var body = JsonConvert.DeserializeObject<AuthResponse>(
				await res.Content.ReadAsStringAsync()
			);

			if (!body.allowed)
			{
				DebugLog.ToConsole("[BackendAuth] Rejected: " + body.reason, MessageType.Error);
				return;
			}

			// tell host we authenticated
			new BackendAuthConfirmMessage(QSBPlayerManager.LocalPlayerId).Send();
		}
		catch (System.Exception e)
		{
			DebugLog.ToConsole("[BackendAuth] Failed: " + e, MessageType.Error);
		}
	}

	private class AuthResponse
	{
		public bool allowed;
		public string reason;
	}
}
