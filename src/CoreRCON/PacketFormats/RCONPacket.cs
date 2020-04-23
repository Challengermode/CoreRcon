using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Encapsulate RCONPacket specification.
///
/// Detailed specification of RCON packets can be found here:
/// https://developer.valvesoftware.com/wiki/Source_RCON_Protocol
/// </summary>
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
            if (size > buffer.Length - 4) throw new InvalidDataException("Packet size specified was larger then buffer");

            if (size < 10) throw new InvalidDataException("Packet received was invalid.");

            int id = BitConverter.ToInt32(buffer, 4);
            PacketType type = (PacketType)BitConverter.ToInt32(buffer, 8);

            try
            {
                // Some games support UTF8 payloads, ASCII will also work due to backwards compatiblity
                char[] rawBody = Encoding.UTF8.GetChars(buffer, 12, size - 10);
                string body = new string(rawBody).TrimEnd();
                // Force Line endings to match environment
                body = Regex.Replace(body, @"\r\n|\n\r|\n|\r", "\r\n");
                return new RCONPacket(id, type, body);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{DateTime.Now} - Error reading RCON packet body exception was: {ex.Message}");
                return new RCONPacket(id, type, "");
            }
        }

        /// <summary>
        /// Serializes a packet to a byte array for transporting over a network.  Body is serialized as UTF8.
        /// </summary>
        /// <returns>Byte array with each field.</returns>
        internal byte[] ToBytes()
        {
            //Should also be compatible with ASCII only servers
            byte[] body = Encoding.UTF8.GetBytes(Body + "\0");
            int bodyLength = body.Length;

            using (var packet = new MemoryStream(12 + bodyLength))
            {
                packet.Write(BitConverter.GetBytes(9 + bodyLength), 0, 4);
                packet.Write(BitConverter.GetBytes(Id), 0, 4);
                packet.Write(BitConverter.GetBytes((int)Type), 0, 4);
                packet.Write(body, 0, bodyLength);
                packet.Write(new byte[] { 0 }, 0, 1);

                return packet.ToArray();
            }
        }
    }
}