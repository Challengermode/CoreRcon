using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CoreRCON.PacketFormats
{
	public class RCONPacket
	{
		public string Body { get; private set; }
		public int Id { get; private set; }
		public PacketType Type { get; private set; }

		/// <summary>
		/// Create a new packet.
		/// </summary>
		/// <param name="id">Some kind of identifier to keep track of responses from the server.</param>
		/// <param name="type">What the server is supposed to do with the body of this packet.</param>
		/// <param name="body">The actual information held within.</param>
		public RCONPacket(int id, PacketType type, string body)
		{
			Id = id;
			Type = type;
			Body = body;
		}

		public override string ToString() => Body;

		/// <summary>
		/// Converts a buffer to a packet.
		/// </summary>
		/// <param name="buffer">Buffer to read.</param>
		/// <returns>Created packet.</returns>
		internal static RCONPacket FromBytes(byte[] buffer)
		{
			if (buffer == null) throw new NullReferenceException("Byte buffer cannot be null.");
			if (buffer.Length < 4) throw new InvalidDataException("Buffer does not contain a size field.");
			if (buffer.Length > Constants.MAX_PACKET_SIZE) throw new InvalidDataException("Buffer is too large for an RCON packet.");

			int size = BitConverter.ToInt32(buffer, 0);

			if (size < 10) throw new InvalidDataException("Packet received was invalid.");

			int id = BitConverter.ToInt32(buffer, 4);
			PacketType type = (PacketType)BitConverter.ToInt32(buffer, 8);

			try
			{
				// Force string to \r\n line endings
				char[] rawBody = Encoding.UTF8.GetChars(buffer, 12, size - 10);
				string body = new string(rawBody, 0, size - 10).TrimEnd();
				body = Regex.Replace(body, @"\r\n|\n\r|\n|\r", "\r\n");
				return new RCONPacket(id, type, body);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"{DateTime.Now} - Error reading RCON packet from server: " + ex.Message);
				return new RCONPacket(id, type, "");
			}
		}

		/// <summary>
		/// Serializes a packet to a byte array for transporting over a network.  Body is serialized as UTF8.
		/// </summary>
		/// <returns>Byte array with each field.</returns>
		internal byte[] ToBytes()
		{
			byte[] body = Encoding.UTF8.GetBytes(Body + "\0");
			int bl = body.Length;

			using (var packet = new MemoryStream(12 + bl))
			{
				packet.Write(BitConverter.GetBytes(9 + bl), 0, 4);
				packet.Write(BitConverter.GetBytes(Id), 0, 4);
				packet.Write(BitConverter.GetBytes((int)Type), 0, 4);
				packet.Write(body, 0, bl);
				packet.Write(new byte[] { 0 }, 0, 1);

				return packet.ToArray();
			}
		}
	}
}