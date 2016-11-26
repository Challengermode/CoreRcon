using System;

namespace CoreRCON
{
	/// <summary>
	/// Basically just another Exception.
	/// </summary>
	public class AuthenticationException : Exception
	{
		public AuthenticationException()
		{
		}

		public AuthenticationException(string message) : base(message)
		{
		}

		public AuthenticationException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}