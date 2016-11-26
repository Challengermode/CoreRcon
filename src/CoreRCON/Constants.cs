namespace CoreRCON
{
	internal class Constants
	{
		/// <summary>
		/// No-op command to send to the server.  Should be easily identifiable!
		/// </summary>
		internal const string CHECK_STR = "//OSW";

		/// <summary>
		/// The maximum size of an RCON packet.
		/// </summary>
		internal const int MAX_PACKET_SIZE = 4096;
	}
}