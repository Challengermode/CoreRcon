namespace CoreRCON.PacketFormats
{
	public enum ServerEnvironment
	{
		Linux = 0x6C,
		Windows = 0x77,
		Mac = 0x6F
	}

	public enum ServerType
	{
		Dedicated = 0x64,
		NonDedicated = 0x6C,
		SourceTV = 0x70
	}

	public enum ServerVAC
	{
		Unsecured = 0x0,
		Secured = 0x1
	}

	public enum ServerVisibility
	{
		Public = 0x0,
		Private = 0x1
	}

	public class SourceQueryInfo : IQueryInfo
    {
		public byte Bots { get; private set; }
		public ServerEnvironment Environment { get; private set; }
		public string Folder { get; private set; }
		public string Game { get; private set; }
		public short GameId { get; private set; }
		public string Map { get; private set; }
		public byte MaxPlayers { get; private set; }
		public string Name { get; private set; }
		public byte Players { get; private set; }
		public byte ProtocolVersion { get; private set; }
		public ServerType Type { get; private set; }
		public ServerVAC VAC { get; private set; }
		public ServerVisibility Visibility { get; private set; }

		public static SourceQueryInfo FromBytes(byte[] buffer)
		{
			int i = 6;
			return new SourceQueryInfo
			{
				ProtocolVersion = buffer[4],
				Name = buffer.ReadNullTerminatedString(i, ref i),
				Map = buffer.ReadNullTerminatedString(i, ref i),
				Folder = buffer.ReadNullTerminatedString(i, ref i),
				Game = buffer.ReadNullTerminatedString(i, ref i),
				GameId = buffer.ReadShort(i, ref i),
				Players = buffer[i++],
				MaxPlayers = buffer[i++],
				Bots = buffer[i++],
				Type = (ServerType)buffer[i++],
				Environment = (ServerEnvironment)buffer[i++],
				Visibility = (ServerVisibility)buffer[i++],
				VAC = (ServerVAC)buffer[i++]
			};
		}
	}

	public class ServerQueryPlayer
	{
		public float Duration { get; private set; }
		public string Name { get; private set; }
		public short Score { get; private set; }

		public static ServerQueryPlayer[] FromBytes(byte[] buffer)
		{
			int i = 7;

			ServerQueryPlayer[] players = new ServerQueryPlayer[buffer[5]];

			for (int p = 0; p < players.Length; ++p)
			{
				players[p] = new ServerQueryPlayer
				{
					Name = buffer.ReadNullTerminatedString(i, ref i),
					Score = buffer.ReadShort(i, ref i),
					Duration = buffer.ReadFloat(i + 2, ref i)
				};

				i += 3;
			}

			return players;
		}
	}
}