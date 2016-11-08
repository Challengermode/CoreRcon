﻿using System;

namespace CoreRCON.Parsers
{
	/// <summary>
	/// Holds callbacks non-generically.
	/// </summary>
	internal class ParserContainer
	{
		internal Func<string, bool> IsMatch { get; set; }
		internal Func<string, object> Parse { get; set; }
		internal Action<object> Callback { get; set; }

		/// <summary>
		/// Attempt to parse the line into an object.
		/// </summary>
		/// <param name="line">Single line from the server.</param>
		/// <param name="result">Object to parse into.</param>
		/// <returns>False if the match failed or parsing was unsuccessful.</returns>
		private bool TryParse(string line, out object result)
		{
			if (!IsMatch(line))
			{
				result = null;
				return false;
			}

			result = Parse(line);
			return result != null;
		}

		/// <summary>
		/// Attempt to parse the line, and call the callback if it succeeded.
		/// </summary>
		/// <param name="line">Single line from the server.</param>
		internal void TryCallback(string line)
		{
			object result;
			if (TryParse(line, out result))
			{
				Callback(result);
			}
		}
	}
}