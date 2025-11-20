// more than half of this is AI since web is not for me

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using QSB.Player;
using QSB.Utility;
using OWML.Common;

public static class WebAdminServer
{
	private static HttpListener listener;
	private static Thread serverThread;

	private static readonly string htmlContent = @"
<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>QSB Admin Panel</title>
<style>
:root {
	--bg-color: #1a1a1e;
	--card-color: #2a2a2e;
	--text-color: #e0e0e0;
	--text-muted: #888;
	--accent-color: #00bfff;
	--success-color: #28a745;
	--danger-color: #dc3545;
	--warn-color: #ffc107;
	--border-color: #444;
	--font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
}
body {
	font-family: var(--font-family);
	background: var(--bg-color);
	color: var(--text-color);
	padding: 20px;
	margin: 0;
	line-height: 1.6;
}
h1, h2 {
	color: #fff;
	border-bottom: 2px solid var(--border-color);
	padding-bottom: 10px;
	font-weight: 300;
}
h1 { font-size: 2.5em; }
h2 { font-size: 1.75em; margin-top: 30px; }

.container {
	max-width: 900px;
	margin: 0 auto;
	display: grid;
	grid-template-columns: repeat(auto-fit, minmax(350px, 1fr));
	gap: 20px;
}
.card {
	background: var(--card-color);
	border-radius: 8px;
	padding: 20px;
	box-shadow: 0 4px 12px rgba(0,0,0,0.2);
}
input[type='text'] {
	width: calc(100% - 22px);
	padding: 10px;
	margin-bottom: 10px;
	background: var(--bg-color);
	border: 1px solid var(--border-color);
	color: var(--text-color);
	border-radius: 5px;
	font-size: 1em;
}
button {
	padding: 10px 15px;
	font-size: 1em;
	font-weight: 600;
	border: none;
	border-radius: 5px;
	cursor: pointer;
	color: #fff;
	transition: all 0.2s ease;
	margin: 5px;
}
.btn-primary { background: var(--accent-color); }
.btn-primary:hover { background: #00aae0; }
.btn-danger { background: var(--danger-color); }
.btn-danger:hover { background: #c82333; }
.btn-warn { background: var(--warn-color); color: #222; }
.btn-warn:hover { background: #e0a800; }
.btn-success { background: var(--success-color); }
.btn-success:hover { background: #218838; }
.btn-secondary { background: #6c757d; }
.btn-secondary:hover { background: #5a6268; }

.controls-grid {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 10px;
}
.full-width {
	grid-column: 1 / -1;
}
#players { margin-top: 10px; }
.player {
	margin: 8px 0;
	padding: 10px;
	background: var(--bg-color);
	border-radius: 5px;
	border-left: 3px solid var(--accent-color);
}
</style>
</head>
<body>

<h1>🚀 QSB Host Console</h1>

<div class='container'>
	
	<div class='card'>
		<h2>Chat</h2>
		<input type='text' id='chatMessage' placeholder='Chat as [CONSOLE]'>
		<button class='btn-primary full-width' onclick='sendChat()'>Send</button>
	</div>

	<div class='card'>
		<h2>Player Actions</h2>
		<input type='text' id='playerName' placeholder='Player name'>
		<input type='text' id='reason' placeholder='Reason (optional)'>
		<div class='controls-grid'>
			<button class='btn-danger' onclick='kickPlayer()'>Kick</button>
			<button class='btn-danger' onclick='banPlayer()'>Ban</button>
			<button class='btn-warn' onclick='mutePlayer()'>Mute</button>
			<button class='btn-success' onclick='unmutePlayer()'>Unmute</button>
			<button class='btn-success full-width' onclick='unbanPlayer()'>Unban</button>
		</div>
	</div>

	<div class='card'>
		<h2>Server Actions</h2>
		<div class='controls-grid'>
			<button class='btn-warn' onclick='freezeServer()'>Freeze Server</button>
			<button class='btn-success' onclick='unfreezeServer()'>Unfreeze Server</button>
			<button class='btn-danger full-width' onclick='clearChat()'>Clear All Chat</button>
		</div>
	</div>

	<div class='card'>
		<h2>Players</h2>
		<div id='players'>Loading...</div>
	</div>
	
</div>

<script>
async function fetchPlayers() {
	try {
		const res = await fetch('/players');
		const data = await res.json();
		const playersDiv = document.getElementById('players');
		playersDiv.innerHTML = '';
		if (data.players.length === 0) {
			playersDiv.innerHTML = '<i>No players connected.</i>';
		}
		data.players.forEach(p => {
			const div = document.createElement('div');
			div.textContent = p;
			div.className = 'player';
			playersDiv.appendChild(div);
		});
	} catch (err) { console.error('Failed to fetch players:', err); }
}
async function sendCommand(cmd) {
	if (!cmd) return;
	try {
		await fetch(`/command?cmd=${encodeURIComponent(cmd)}`);
		setTimeout(fetchPlayers, 500);
	} catch (err) { console.error('Failed to send command:', err); }
}

// --- MODIFIED ---
function sendChat() {
	const msg = document.getElementById('chatMessage').value;
	if (!msg) return;
	// This now sends a /say command, which your interpreter can handle.
	// We'll add the [CONSOLE] tag here so it looks right in-game.
	sendCommand(`/say [CONSOLE] ${msg}`);
	document.getElementById('chatMessage').value = '';
}

// --- NEW ---
function clearChat() {
	if (confirm('Are you sure you want to clear the chat for ALL players?')) {
		sendCommand('/clearchat');
	}
}

function kickPlayer() {
	const name = document.getElementById('playerName').value;
	if (!name) return alert('Player name is required.');
	const reason = document.getElementById('reason').value || 'No reason provided';
	sendCommand(`/kick ${name} ${reason}`);
}
function banPlayer() {
	const name = document.getElementById('playerName').value;
	if (!name) return alert('Player name is required.');
	const reason = document.getElementById('reason').value || 'No reason provided';
	sendCommand(`/ban ${name} ${reason}`);
}
function unbanPlayer() {
	const name = document.getElementById('playerName').value;
	if (!name) return alert('Player name is required.');
	sendCommand(`/unban ${name}`);
}
function mutePlayer() {
	const name = document.getElementById('playerName').value;
	if (!name) return alert('Player name is required.');
	const reason = document.getElementById('reason').value || 'No reason provided';
	sendCommand(`/mute ${name} ${reason}`);
}
function unmutePlayer() {
	const name = document.getElementById('playerName').value;
	if (!name) return alert('Player name is required.');
	sendCommand(`/unmute ${name}`);
}
function freezeServer() { sendCommand('/serverfreeze'); }
function unfreezeServer() { sendCommand('/unserverfreeze'); }

setInterval(fetchPlayers, 2000);
fetchPlayers();
</script>
</body>
</html>
";

	public static void Start(int port = 8000)
	{
		serverThread = new Thread(() =>
		{
			try
			{
				listener = new HttpListener();
				listener.Prefixes.Add($"http://127.0.0.1:{port}/"); // ALWAYS 8035 (specified in network manager)
				listener.Start();
				DebugLog.ToConsole($"[WebAdmin] Server started on http://127.0.0.1:{port}", MessageType.Success);

				while (listener.IsListening)
				{
					try
					{
						var context = listener.GetContext();
						ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
					}
					catch (HttpListenerException hle)
					{
						if (hle.ErrorCode == 995) break;
						DebugLog.ToConsole($"[WebAdmin] Listener error: {hle.Message}", MessageType.Error);
					}
					catch (Exception e)
					{
						DebugLog.ToConsole($"[WebAdmin] Listener loop error: {e}", MessageType.Error);
					}
				}
			}
			catch (Exception e)
			{
				DebugLog.ToConsole($"[WebAdmin] Server thread failed to start: {e}", MessageType.Error);
			}
		});
		serverThread.IsBackground = true;
		serverThread.Start();
	}

	private static void HandleRequest(HttpListenerContext context)
	{
		try
		{
			string path = context.Request.Url.AbsolutePath.ToLower();
			string response = "{}";
			string contentType = "application/json";

			if (path == "/" || path.EndsWith("index.html"))
			{
				response = htmlContent;
				contentType = "text/html";
			}
			else if (path == "/players")
			{
				var players = new List<string>();
				foreach (var p in QSBPlayerManager.PlayerList)
					players.Add(p.Name);

				var wrapper = new PlayerListWrapper { players = players.ToArray() };
				response = JsonUtility.ToJson(wrapper);
			}
			else if (path == "/command")
			{
				string cmd = context.Request.QueryString["cmd"];
				if (!string.IsNullOrEmpty(cmd))
				{
					DebugLog.ToConsole($"[WebAdmin] Queuing command: {cmd}", MessageType.Info);
					MainThreadDispatcher.RunOnMainThread(() => CommandInterpreter.InterpretCommand(cmd));
				}
				response = "{\"status\":\"ok\"}";
			}

			byte[] buffer = Encoding.UTF8.GetBytes(response);
			context.Response.ContentType = contentType;
			context.Response.ContentLength64 = buffer.Length;
			context.Response.OutputStream.Write(buffer, 0, buffer.Length);
		}
		catch (Exception e)
		{
			DebugLog.ToConsole($"[WebAdmin] Failed to handle request: {e}", MessageType.Error);
		}
		finally
		{
			context.Response.OutputStream.Close();
		}
	}

	[Serializable]
	private class PlayerListWrapper { public string[] players; }

	public static void Stop()
	{
		try
		{
			if (listener != null && listener.IsListening)
			{
				listener.Stop();
				listener.Close();
				listener = null;
			}

			if (serverThread != null && serverThread.IsAlive)
			{
				// Wait for the thread to finish cleanly instead of aborting
				if (!serverThread.Join(1000))
				{
					serverThread.Interrupt();
				}
				serverThread = null;
			}

			DebugLog.ToConsole("[WebAdmin] Server stopped.", MessageType.Info);
		}
		catch (Exception e)
		{
			DebugLog.ToConsole($"[WebAdmin] Error while stopping server: {e}", MessageType.Error); // this may happen if someone somehow uses the function without being host or the web isnt running (like if port is taken)
		}
	}
}