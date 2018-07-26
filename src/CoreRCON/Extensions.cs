using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CoreRCON
{
    internal static class Extensions
    {
        // Trick VS into thinking this is a resolved task
        internal static void Forget(this Task task)
        {
        }

        /// <summary>
        /// Step through a byte array and read a null-terminated string.
        /// </summary>
        /// <param name="bytes">Byte array.</param>
        /// <param name="start">Offset to start reading from.</param>
        /// <param name="i">Offset variable to move to the end of the string.</param>
        /// <returns>UTF-8 encoded string.</returns>
        public static string ReadNullTerminatedString(this byte[] bytes, int start, ref int i)
        {
            int end = Array.IndexOf(bytes, (byte)0, start);
            if (end < 0) throw new ArgumentOutOfRangeException("Byte array does not appear to contain a null byte to stop reading a string at.");
            i = end + 1;
            return Encoding.UTF8.GetString(bytes, start, end - start);
        }

        public static List<string> ReadNullTerminatedStringArray(this byte[] bytes, int start, ref int i)
        {
            var result = new List<string>();
            var byteindex = start;
            while (bytes[byteindex] != 0x00)
            {
                result.Add(ReadNullTerminatedString(bytes, byteindex, ref byteindex));
            }
            i = byteindex + 1;
            return result;
        }

        public static Dictionary<string, string> ReadNullTerminatedStringDictionary(this byte[] bytes, int start, ref int i)
        {
            var result = new Dictionary<string, string>();
            var byteindex = start;
            while (bytes[byteindex] != 0x00)
            {
                result.Add(ReadNullTerminatedString(bytes, byteindex, ref byteindex), ReadNullTerminatedString(bytes, byteindex, ref byteindex));
            }
            i = byteindex + 1;
            return result;
        }

        /// <summary>
        /// Read a short from a byte array and update the offset.
        /// </summary>
        /// <param name="bytes">Byte array.</param>
        /// <param name="start">Offset to start reading from.</param>
        /// <param name="i">Offset variable to move to the end of the string.</param>
        public static short ReadShort(this byte[] bytes, int start, ref int i)
        {
            i += 2;
            return BitConverter.ToInt16(bytes, start);
        }

        /// <summary>
        /// Read a float from a byte array and update the offset.
        /// </summary>
        /// <param name="bytes">Byte array.</param>
        /// <param name="start">Offset to start reading from.</param>
        /// <param name="i">Offset variable to move to the end of the string.</param>
        public static float ReadFloat(this byte[] bytes, int start, ref int i)
        {
            i += 4;
            return BitConverter.ToSingle(bytes, start);
        }

        /// <summary>
        /// Truncate a string to a maximum length.
        /// </summary>
        /// <param name="str">String to truncate.</param>
        /// <param name="maxLength">Maximum length of the string.</param>
        /// <returns>Truncated string with ellipses, or the original string.</returns>
        internal static string Truncate(this string str, int maxLength)
        {
            return str?.Length <= maxLength
                ? str
                : str.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Receives a block of memory asyncronosly 
        /// </summary>
        /// <param name="socket">Socket to receive from</param>
        /// <param name="memory">Memory segment to receive to</param>
        /// <param name="socketFlags">Flags for socket</param>
        /// <returns>Awaitable task resolving to the number of bytes received</returns>
        public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags)
        {
            var arraySegment = GetArray(memory);
            return SocketTaskExtensions.ReceiveAsync(socket, arraySegment, socketFlags);
        }

        public static string GetString(this Encoding encoding, ReadOnlyMemory<byte> memory)
        {
            var arraySegment = GetArray(memory);
            return encoding.GetString(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
        }

        private static ArraySegment<byte> GetArray(Memory<byte> memory)
        {
            return GetArray((ReadOnlyMemory<byte>)memory);
        }

        private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory)
        {
            if (!MemoryMarshal.TryGetArray(memory, out var result))
            {
                throw new InvalidOperationException("Buffer backed by array was expected");
            }

            return result;
        }
    }
}
