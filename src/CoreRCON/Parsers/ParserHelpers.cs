using System;
using System.Reflection;
using System.Linq;

namespace CoreRCON.Parsers
{
	internal static class ParserHelpers
	{
		internal static IParser<T> GetParser<T>()
			where T : class, new()
		{
			var implementor = (new T()).GetType().GetTypeInfo().Assembly.GetTypes().FirstOrDefault(t => t.GetInterfaces().Contains(typeof(IParser<T>)));
			if (implementor == null) throw new ArgumentException($"A class implementing {nameof(IParser)}<{typeof(T).FullName}> was not found in the assembly.");
			return (IParser<T>)Activator.CreateInstance(implementor);
		}
	}
}
