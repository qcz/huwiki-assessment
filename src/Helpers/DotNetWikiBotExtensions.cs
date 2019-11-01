using DotNetWikiBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace AssessmentDotNet.Helpers
{
	public static class DotNetWikiBotExtensions
	{
		public static void SaveWithRetries(this Page page, string newText, string comment, bool isMinorEdit)
		{
			int tries = 0;

			while (tries < 10)
			{
				if (tries > 0)
				{
					Console.WriteLine("Oldal mentése: {0}. próbálkozás...", tries);
				}

				try
				{
					page.Save(newText, comment, isMinorEdit);
					break;
				}
				catch (WikiBotException wikiex) when (wikiex.Message.IndexOf("read-only mode") != -1)
				{
					tries++;

					if (tries == 10)
						throw;

					Thread.Sleep(1000);
				}
				catch (WebException wex)
				{
					tries++;

					if (tries == 10)
						throw;

					HttpStatusCode? status = (wex.Response as HttpWebResponse)?.StatusCode;
					if (status.HasValue == false || status.Value != HttpStatusCode.ServiceUnavailable)
						throw;

					Thread.Sleep(1000);
				}
			}
		}
	}
}
