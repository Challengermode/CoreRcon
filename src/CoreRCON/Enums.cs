namespace CoreRCON
{
	// SERVERDATA_AUTH_RESPONSE and SERVERDATA_EXECCOMMAND are both 2
	public enum PacketType
	{
		// SERVERDATA_RESPONSE_VALUE
		Response = 0,

		// SERVERDATA_AUTH_RESPONSE
		AuthResponse = 2,

		// SERVERDATA_EXECCOMMAND
		ExecCommand = 2,

		// SERVERDATA_AUTH
		Auth = 3
	}
}