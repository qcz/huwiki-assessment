using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DotNetWikiBot;

namespace AssessmentDotNet
{
	class Article : IComparable<Article>
	{
		public string Name { get; set; } = "";
		public DateTime Date { get; set; }
		public Quality Quality { get; set; }
		public Importance Importance { get; set; }
		public string OldId { get; set; } = "";

		public int CompareTo(Article other)
		{
			Article art = other as Article;
			if (art == null)
				return 0;

			if (this.Importance == null && art.Importance != null) return 1;
			if (this.Importance != null && art.Importance == null) return -1;

			if (this.Importance != null &&
				art.Importance != null &&
				this.Importance.Value.CompareTo(art.Importance.Value) != 0)
			{
				return this.Importance.Value.CompareTo(art.Importance.Value) * -1;
			}

			if (this.Quality.Value.CompareTo(art.Quality.Value) != 0)
			{
				if (this.Quality.Value == 0) return 1;
				if (art.Quality.Value == 0) return -1;
				return this.Quality.Value.CompareTo(art.Quality.Value);
			}

			return String.Compare(this.Name, art.Name, true, new CultureInfo("hu-HU"));
		}
	}
}