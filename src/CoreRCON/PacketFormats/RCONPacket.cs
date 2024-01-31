using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
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
    /// <summary>
    /// Create a new packet.
    /// </summary>
    /// <param name="id">Some kind of identifier to keep track of responses from the server.</param>
    /// <param name="type">What the server is supposed to do with the body of this packet.</param>
    /// <param name="body">The actual information held within.</param>
    public class RCONPacket(int id, PacketType type, string body)
    {
        public string Body { get; private set; } = body;
        public int Id { get; private set; } = id;
        public PacketType Type { get; private set; } = type;

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

            ReadOnlySpan<byte> bufferSpan = buffer;
            int size = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(0, 4));
            if (size > buffer.Length - 4) throw new InvalidDataException("Packet size specified was larger then buffer");

            if (size < 10) throw new InvalidDataException("Packet received was invalid.");

            int id = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(4, 4));
            PacketType type = (PacketType)BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(8, 4));

            try
            {
                // Some games support UTF8 payloads, ASCII will also work due to backwards compatiblity
                string body = Encoding.UTF8.GetString(buffer, 12, size - 10)
                    .TrimEnd()
                    .Normalize();

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
            int bodyLength = Encoding.UTF8.GetByteCount(Body);
            //
            int packetSize = Constants.PACKET_HEADER_SIZE + Constants.PACKET_PADDING_SIZE + bodyLength;
            
            byte[] packetBytes = new byte[packetSize];
            Span<byte> packetSpan = packetBytes;

            // Write packet size
            // Packet size parameter does not include the size of the size parameter itself
            var normalizedPacketSize = packetSize - 4;
            BinaryPrimitives.WriteInt32LittleEndian(packetSpan, normalizedPacketSize); 
            packetSpan = packetSpan.Slice(4);

            // Write ID
            BinaryPrimitives.WriteInt32LittleEndian(packetSpan, Id);
            packetSpan = packetSpan.Slice(4);

            // Write type
            BinaryPrimitives.WriteInt32LittleEndian(packetSpan, (int)Type);

            // Write body
            Encoding.UTF8.GetBytes(Body, 0, Body.Length, packetBytes, Constants.PACKET_HEADER_SIZE);
            
            packetBytes[packetBytes.Length - 2] = 0; // Null terminator for the body
            packetBytes[packetBytes.Length - 1] = 0; // Null terminator for the package

            return packetBytes;
        }
    }
}
