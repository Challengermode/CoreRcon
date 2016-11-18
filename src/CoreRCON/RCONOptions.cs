using System.Net;

namespace CoreRCON
{
	/// <summary>
	/// Object to pass values into the RCON class.
	/// </summary>
	public class RCONOptions
	{
		public IPAddress ServerHost { get; set; }
		public ushort ServerPort { get; set; }
		public string Password { get; set; }

		/// <summary>
		/// Milliseconds to wait between checking if the server is still responding.  Defaults to 30 seconds (30000 ms).
		/// </summary>
		public uint DisconnectionCheckInterval { get; set; } = 30000;

		/// <summary>
		/// If you want to receive logaddress logs, enable this.
		/// </summary>
		public bool EnableLogging { get; set; } = false;
		public IPAddress LogHost { get; set; }
		public ushort LogPort { get; set; } = 0;
	}
}