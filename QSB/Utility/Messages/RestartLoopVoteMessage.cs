using QSB.HUD;
using QSB.Messaging;
using UnityEngine;

namespace QSB.Utility.Messages;

public class RestartLoopVoteRequestMessage : QSBMessage<string>
{
	public RestartLoopVoteRequestMessage(string command) : base(command) => To = 0;

	public override void OnReceiveLocal() => OnReceiveRemote();

	public override void OnReceiveRemote()
		=> RestartLoopVoteManager.Instance?.HandleRequest(From, Data);
}

public class RestartLoopVoteNoticeMessage : QSBMessage<string>
{
	public RestartLoopVoteNoticeMessage(string message) : base(message) { }

	public override void OnReceiveLocal() => OnReceiveRemote();

	public override void OnReceiveRemote()
		=> MultiplayerHUDManager.Instance.WriteSystemMessage(Data, Color.yellow);
}
