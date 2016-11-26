using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public class Status : IParseable
	{
		public string Account { get; set; }
		public byte Bots { get; set; }
		public ulong CommunityID { get; set; }
		public string Hostname { get; set; }
		public byte Humans { get; set; }
		public string LocalHost { get; set; }
		public string Map { get; set; }
		public byte MaxPlayers { get; set; }
		public string PublicHost { get; set; }
		public string SteamID { get; set; }
		public string[] Tags { get; set; }
		public string Version { get; set; }
	}

	internal class StatusParser : DefaultParser<Status>
	{
		public override string Pattern { get; } = @"hostname\s*: (?<Hostname>.+?)
version\s*: (?<Version>.+?)
udp\/ip\s*: (?<LocalHost>.+?)  \(public ip: (?<PublicHost>.+?)\)
steamid\s*: \[(?<SteamID>.+?)\] \((?<CommunityID>\d+)\)
account\s*: (?<Account>.+?)
map\s*: (?<Map>.+?) at: .*
tags\s*: (?<Tags>.+?)
players\s*: (?<Humans>\d+) humans, (?<Bots>\d+) bots \((?<MaxPlayers>\d+) max\)";

		public override Status Load(GroupCollection groups)
		{
			return new Status
			{
				Hostname = groups["Hostname"].Value,
				Version = groups["Version"].Value,
				LocalHost = groups["LocalHost"].Value,
				PublicHost = groups["PublicHost"].Value,
				SteamID = groups["SteamID"].Value,
				CommunityID = ulong.Parse(groups["CommunityID"].Value),
				Account = groups["Account"].Value,
				Map = groups["Map"].Value,
				Tags = groups["Tags"].Value.Split(','),
				Humans = byte.Parse(groups["Humans"].Value),
				Bots = byte.Parse(groups["Bots"].Value),
				MaxPlayers = byte.Parse(groups["MaxPlayers"].Value)
			};
		}
	}
}