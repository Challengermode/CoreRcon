using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public enum MessageChannel
	{
		Team,
		All
	}

	public class ChatMessage : IParseable
	{
		public MessageChannel Channel { get; set; }
		public string Message { get; set; }
		public Player Player { get; set; }
	}

	public class ChatMessageParser : DefaultParser<ChatMessage>
	{
		public override string Pattern { get; } = $"(?<Sender>{playerParser.Pattern}) (?<Channel>say_team|say) \"(?<Message>.+?)\"";
		private static PlayerParser playerParser { get; } = new PlayerParser();

		public override ChatMessage Load(GroupCollection groups)
		{
			return new ChatMessage
			{
				Player = playerParser.Parse(groups["Sender"]),
				Message = groups["Message"].Value,
				Channel = groups["Channel"].Value == "say" ? MessageChannel.All : MessageChannel.Team
			};
		}
	}
}