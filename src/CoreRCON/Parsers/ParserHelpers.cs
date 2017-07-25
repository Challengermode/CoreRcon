using System;
using System.Linq;
using System.Reflection;

namespace CoreRCON.Parsers
{
	internal static class ParserHelpers
	{
		internal static IParser<T> CreateParser<T>()
			where T : class, IParseable, new()
		{
			var implementor = new T().GetType().GetTypeInfo().Assembly.GetTypes().FirstOrDefault(t => t.GetTypeInfo().GetInterfaces().Contains(typeof(IParser<T>)));
			if (implementor == null) throw new ArgumentException($"A class implementing {nameof(IParser)}<{typeof(T).FullName}> was not found in the assembly.");
			return (IParser<T>)Activator.CreateInstance(implementor);
		}
	}
}