using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CoreRCON.PacketFormats
{
	// Structure of a LogAddress packet from SRCDS:
	// 255 255 255 255
	// 82 or 83 (82 = no password, 83 = password)
	// if 82, the rest of the packet is the body.
	// Not sure what happens if it's 83, since I can't get my test server to return one even with sv_logsecret set.

	public struct LogAddressPacket
	{
		/// <summary>
		/// [UNSUPPORTED] If the packet was sent with sv_logsecret set.
		/// </summary>
		public readonly bool HasPassword;

		/// <summary>
		/// The raw body of the packet.
		/// </summary>
		public readonly string RawBody;

		/// <summary>
		/// The body of the packet with the timestamp removed.
		/// </summary>
		public readonly string Body;

		/// <summary>
		/// The timestamp at which the packet was sent (not received).
		/// </summary>
		public readonly DateTime Timestamp;

		/// <summary>
		/// Create a new packet.
		/// </summary>
		/// <param name="hasPassword">[UNSUPPORTED] If the server returned this packet with sv_logsecret set.</param>
		/// <param name="rawBody">The raw body from the packet.</param>
		public LogAddressPacket(bool hasPassword, string rawBody)
		{
			HasPassword = hasPassword;
			RawBody = rawBody;

			// Get timestamp
			// https://developer.valvesoftware.com/wiki/HL_Log_Standard
			var match = new Regex(@"L (\d{2}/\d{2}/\d{4} - \d{2}:\d{2}:\d{2}):").Match(rawBody);
			if (!match.Success)
			{
				Timestamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			}
			else
			{
				var value = match.Groups[1].Value;
				Timestamp = DateTime.ParseExact(value, "MM/dd/yyyy - HH:mm:ss", CultureInfo.InvariantCulture);
			}

			// Get body without the date/time
			Body = rawBody.Substring(25);
		}

		/// <summary>
		/// Converts a buffer to a packet.
		/// </summary>
		/// <param name="buffer">Buffer to read.</param>
		/// <returns>Created packet.</returns>
		internal static LogAddressPacket FromBytes(byte[] buffer)
		{
			if (buffer.Length < 7) throw new InvalidDataException("LogAddress packet is of an invalid length.");
			if (!buffer.Take(4).SequenceEqual(new byte[] { 255, 255, 255, 255 })) throw new InvalidDataException("LogAddress packet does not contain a valid header.");

			bool hasPassword = buffer[5] == 83;
			string body = new string(Encoding.UTF8.GetChars(buffer, 5, buffer.Length - 7));

			return new LogAddressPacket(hasPassword, body);
		}

		public override string ToString() => RawBody;
	}
}