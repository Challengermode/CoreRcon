using System.Text.RegularExpressions;

namespace CoreRCON.Parsers
{
	/// <summary>
	/// Default implementation of IParser(T)
	/// </summary>
	/// <typeparam name="T">Type of object the parser returns.</typeparam>
	public abstract class DefaultParser<T> : IParser<T>
		where T : class, IParseable
	{
		public abstract string Pattern { get; }

		public virtual bool IsMatch(string input) => new Regex(Pattern, RegexOptions.Singleline).IsMatch(input);

		public abstract T Load(GroupCollection groups);

		public virtual T Parse(Group group) => Parse(group.Value);

		public T Parse(string input)
		{
			var groups = new Regex(Pattern, RegexOptions.Singleline).Match(input).Groups;
			return Load(groups);
		}
	}
}