using Mirror;
using Newtonsoft.Json;
using OWML.Common;
using QSB;
using QSB.Localization;
using QSB.Menus;
using QSB.Player;
using QSB.Utility;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class LobbyReporter
{
	private static readonly HttpClient http = new();

	// lobby id to track
	private static string lobbyId = null;
	// DO NOT SHARE THIS WITH ANYBODY
	// THIS WILL ALLOW A MALICIOUS ACTOR TO CONTROL YOUR SERVER
	// this is securely stored on the server AND your host client
	private static string secretKey = null;
	// heartbeat tracking
	private static bool heartbeatActive = false;
	// heart beat timer to prevent getting removed
	private static float heartbeatTimer = 0f;

	private static async Task<string> GetPlayerRegionAsync()
	{
		try
		{
			using var client = new HttpClient();
			string ip = await client.GetStringAsync("https://api.ipify.org"); // get public IP
			string geoUrl = $"http://ip-api.com/json/{ip}";
			string geoJson = await client.GetStringAsync(geoUrl);
			var geoData = JsonConvert.DeserializeObject<GeoResponse>(geoJson);
			return geoData.regionName;
		}
		catch
		{
			return "Unknown";
		}
	}

	private class GeoResponse
	{
		public string country;
		public string regionName;
		public string city;
	}


	//add lobby
	public static async void RegisterLobby()
	{
		string steamId = SteamManager.Initialized
			? Steamworks.SteamUser.GetSteamID().ToString()
			: "unknown";

		string region = await GetPlayerRegionAsync();

		var payload = new
		{
			steamId = steamId,
			ip = "NaN", // currently removed for security
			port = QSBCore.KcpPort,
			players = QSBPlayerManager.PlayerList.Count,
			maxPlayers = 8,
			version = QSBCore.QSBVersion,
			region = region
		};

		try
		{
			string json = JsonConvert.SerializeObject(payload);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var result = await http.PostAsync("https://server.ctksystem.com/lobby/register", content);
			string responseText = await result.Content.ReadAsStringAsync();

			if (!result.IsSuccessStatusCode)
			{
				MenuManager.ErrorPopup.Show($"Failed to register lobby to the website (YOUR FRIENDS CAN STILL JOIN): {responseText}");
				return;
			}

			var response = JsonConvert.DeserializeObject<RegisterResponse>(responseText);

			if (string.IsNullOrEmpty(response.lobbyId) || string.IsNullOrEmpty(response.secretKey))
			{
				MenuManager.ErrorPopup.Show($"Lobby registration failed to the website (YOUR FRIENDS CAN STILL JOIN): {responseText}");
				return;
			}

			lobbyId = response.lobbyId;
			secretKey = response.secretKey;
			heartbeatActive = true;

			DebugLog.ToConsole($"[LobbyReporter] Lobby registered: {lobbyId}", MessageType.Info);
		}
		catch (Exception ex)
		{
			MenuManager.ErrorPopup.Show($"RegisterLobby error to the website (YOUR FRIENDS CAN STILL JOIN): {ex.Message}");
			DebugLog.ToConsole("[LobbyReporter] RegisterLobby error: " + ex, MessageType.Error);
		}

	}

	// heartbeat
	public static async void Heartbeat()
	{
		if (!heartbeatActive || lobbyId == null || secretKey == null)
			return;

		// get players
		int currentPlayers = NetworkServer.connections.Count;

		var payload = new
		{
			lobbyId = lobbyId,
			secretKey = secretKey,
			players = currentPlayers
		};

		try
		{
			var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
			await http.PostAsync("https://server.ctksystem.com/lobby/heartbeat", content);
		}
		catch
		{
			// we don't really care
		}
	}


	// remove lobby (on disconnect)
	public static async void DeregisterLobby()
	{
		if (lobbyId == null || secretKey == null)
			return;

		var payload = new
		{
			lobbyId = lobbyId,
			secretKey = secretKey
		};

		try
		{
			var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
			await http.PostAsync("https://server.ctksystem.com/lobby/remove", content);
		}
		catch { }

		heartbeatActive = false;
		lobbyId = null;
		secretKey = null;
	}

	// update loop
	public static void Update()
	{
		if (!heartbeatActive) return;

		heartbeatTimer += Time.deltaTime;
		if (heartbeatTimer >= 10f)
		{
			heartbeatTimer = 0f;
			Heartbeat();
		}
	}

	// response from server
	public class RegisterResponse
	{
		public string lobbyId;
		public string secretKey;
	}
}
