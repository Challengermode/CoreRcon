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

        private Regex _regex_compiled => new Regex(Pattern, RegexOptions.Compiled | RegexOptions.Singleline);

        public virtual bool IsMatch(string input) => _regex_compiled.IsMatch(input);

        public abstract T Load(GroupCollection groups);

        public virtual T Parse(Group group) => Parse(group.Value);

        public T Parse(string input)
        {
            var groups = _regex_compiled.Match(input).Groups;
            return Load(groups);
        }
    }
}