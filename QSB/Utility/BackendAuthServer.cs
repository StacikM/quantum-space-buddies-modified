using Newtonsoft.Json;
using QSB.Messaging;
using QSB.Player;
using QSB.Player.Messages;
using QSB.Utility;
using System.Net.Http;
using System.Text;

public static class BackendAuthServer
{
	private static readonly HttpClient http = new();

	public static async void BanPlayer(PlayerInfo player, string reason)
	{
		if (!BackendAuthManager.Enabled)
			return;

		var payload = new
		{
			lobbyId = LobbyReporter.lobbyId,
			secretKey = LobbyReporter.secretKey,
			playerId = player.PlayerId,
			reason
		};

		var content = new StringContent(
			JsonConvert.SerializeObject(payload),
			Encoding.UTF8,
			"application/json"
		);

		await http.PostAsync(
			"https://server.ctksystem.com/auth/ban",
			content
		);

		new PlayerKickMessage(
			player.PlayerId,
			$"Server banned: {reason}"
		).Send();
	}
}
