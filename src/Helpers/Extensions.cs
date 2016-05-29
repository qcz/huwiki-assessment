using System;
using System.IO;
using System.Xml;

namespace AssessmentDotNet
{
	static class Extensions
	{
		public static string ToLowerFirst(this string s)
		{
			if (s == null) return null;
			if (s == String.Empty) return s;
			if (s.Length == 1) return s.ToLower();
			return s[0].ToString().ToLower() + s.Substring(1);
		}

		public static string ToUpperFirst(this string s)
		{
			if (s == null) return null;
			if (s == String.Empty) return s;
			if (s.Length == 1) return s.ToUpper();
			return s[0].ToString().ToUpper() + s.Substring(1);
		}

		public static string XmlToString(this XmlDocument xd)
		{
			using (StringWriter sw = new StringWriter())
			{
				using (XmlTextWriter xw = new XmlTextWriter(sw))
				{
					xw.Formatting = Formatting.Indented;
					xd.WriteTo(xw);
					return sw.ToString();
				}
			}
		}
	}

}