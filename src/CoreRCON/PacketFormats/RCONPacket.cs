using System;
using System.Buffers;

using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

/// <summary>
/// Encapsulate RconPacket specification.
///
/// Detailed specification of RCON packets can be found here:
/// https://developer.valvesoftware.com/wiki/Source_RCON_Protocol
/// </summary>
namespace CoreRCON.PacketFormats
{
    public static class RconPacketExtensions
    {
        public static byte[] ToBytes(this RconPacket packet, Encoding encoding)
        {
            //Should also be compatible with ASCII only servers
            int bodyLength = encoding.GetByteCount(packet.Body);

            byte[] buffer = new byte[Constants.MIN_PACKET_SIZE + bodyLength];

            using var stream = new MemoryStream(buffer);
            using var writer = new BinaryWriter(stream, encoding);

            writer.Write(Constants.HEADER_SIZE + bodyLength); // Size of rest of the message
            writer.Write(packet.Id); // ID
            writer.Write((int)packet.Type); // Type

            // Manually writing message body to the byte array because BinaryWriter#Write(string value)
            // method prefixes the output with the string length which we do not want here.
            encoding.GetBytes(packet.Body, buffer.AsSpan()[(int)stream.Position..]);
            stream.Position += bodyLength;

            writer.Write((byte)0); // Null-terminated string
            writer.Write((byte)0); // Message Termination

            return buffer;
        }

    }

    /// <summary>
    /// Create a new packet.
    /// </summary>
    /// <param name="id">Some kind of identifier to keep track of responses from the server.</param>
    /// <param name="type">What the server is supposed to do with the body of this packet.</param>
    /// <param name="body">The actual information held within.</param>
    public class RconPacket(int id, PacketType type, string body)
    {
        public int Id { get; private set; } = id;
        public PacketType Type { get; private set; } = type;
        public string Body { get; private set; } = body;
        public bool IsTermination
        {
            get
            {
                return Encoding.UTF8.GetBytes(Body).AsSpan().IsEmpty;
            }
        }

        public RconPacket Termination => Create(Id, PacketType.Response, "\0");

        public static RconPacket Create(int id, PacketType type, string body) => new(id, type, body);
        public override string ToString() => Body;

        /// <summary>
        /// Converts a buffer to a packet.
        /// </summary>
        /// <param name="buffer">Buffer to read.</param>
        /// <returns>Created packet.</returns>
        internal static RconPacket FromBytes(ReadOnlySequence<byte> sequence, Encoding encoding)
        {
            if (sequence.IsEmpty) throw new NullReferenceException("Byte buffer cannot be null.");
            if (sequence.Length < 4) throw new InvalidDataException("Buffer does not contain a size field.");

            SequenceReader<byte> reader = new(sequence);
            reader.TryReadLittleEndian(out int length);
            reader.TryReadLittleEndian(out int id);
            reader.TryReadLittleEndian(out int type);
            reader.TryReadTo(span: out var span, delimiter: (byte)0, advancePastDelimiter: false);

            string body = encoding.GetString(span);

            return new RconPacket(id, (PacketType)type, body);
        }
    }
}
