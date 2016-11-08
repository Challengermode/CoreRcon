using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Standard
{
	public class Status
	{
		public string Hostname { get; set; }
	}

	internal class StatusParser : DefaultParser<Status>
	{
		public override string Pattern { get; } = @"hostname: (?<Hostname>.+?)\n";

		public override Status Load(GroupCollection groups)
		{
			return new Status
			{
				Hostname = groups["Hostname"].Value
			};
		}
	}
}