namespace CoreRCON
{
	public partial class CoreRCON
	{
		// Since packets containing the special check string are thrown out, add some randomness to it so users can't add the check string to their name and fly under-the-radar.
		private static string _identifier;
	}
}