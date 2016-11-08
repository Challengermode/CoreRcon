﻿using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	/// <summary>
	/// Default implementation of IParser<T>
	/// </summary>
	/// <typeparam name="T">Type of object the parser returns.</typeparam>
	public abstract class DefaultParser<T> : IParser<T>
		where T : class
	{
		public abstract string Pattern { get; }
		public abstract T Load(GroupCollection groups);
		public virtual bool IsMatch(string input) => new Regex(Pattern).IsMatch(input);
		public virtual T Parse(Group group) => Parse(group.Value);
		public T Parse(string input)
		{
			var groups = new Regex(Pattern).Match(input).Groups;
			return Load(groups);
		}
	}
}