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
	}
}