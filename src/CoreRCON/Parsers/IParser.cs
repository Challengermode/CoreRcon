using System.Text.RegularExpressions;

namespace CoreRCON.Parsers
{
	// Purely for type constraints
	public interface IParseable { }

	public interface IParser { }

	public interface IParser<T> : IParser
		where T : class
	{
		/// <summary>
		/// Regex pattern used to match this item.
		/// </summary>
		string Pattern { get; }

		/// <summary>
		/// Returns if the line received from the server can be parsed into the desired type.
		/// </summary>
		/// <param name="input">Single line from the server.</param>
		bool IsMatch(string input);

		/// <summary>
		/// Allows the parser to be called from another parser that included this parser's pattern.
		/// </summary>
		/// <param name="groups">GroupCollection returned by the other parser.</param>
		T Load(GroupCollection groups);

		/// <summary>
		/// Parses the line from the server into the desired type.
		/// </summary>
		/// <param name="input">Single line from the server.</param>
		T Parse(string input);

		/// <summary>
		/// Convenience method for nested capture groups.
		/// </summary>
		/// <param name="group">Group with .Value property to read.</param>
		T Parse(Group group);
	}
}