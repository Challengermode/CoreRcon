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
        /// (Officially 4096, larger packet where recived when running cvarlist)
        /// </summary>
        internal const int MAX_PACKET_SIZE = 4200;

        /// <summary>
        /// Minimum size of a rcon packet
        /// </summary>
        internal const int MIN_PACKET_SIZE = 14;
    }
}