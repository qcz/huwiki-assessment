using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace AssessmentDotNet
{
	public class WebHelpers
	{
		public static string GetUrlContent(string pageUrl)
		{
			HttpWebRequest webReq = null;
			HttpWebResponse webResp = null;
			webReq = (HttpWebRequest)WebRequest.Create(pageUrl);
			webReq.Proxy.Credentials = CredentialCache.DefaultCredentials;
			webReq.KeepAlive = false;
			webReq.Method = "GET";
			webReq.ProtocolVersion = HttpVersion.Version11;
			webReq.UserAgent = "AssessmentDotNet (DotNetWikiBot)";
			webReq.CookieContainer = new CookieContainer();
			webReq.Timeout = 10000;
			webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
			webReq.UseDefaultCredentials = true;
			
			using (webResp = (HttpWebResponse)webReq.GetResponse())
			{
				Stream respStream = webResp.GetResponseStream();

				if (webResp.ContentEncoding.ToLower().Contains("gzip"))
					respStream = new GZipStream(respStream, CompressionMode.Decompress);
				else if (webResp.ContentEncoding.ToLower().Contains("deflate"))
					respStream = new DeflateStream(respStream, CompressionMode.Decompress);

				using (var memoryStream = new MemoryStream())
				{
					respStream.CopyTo(memoryStream);
					return Encoding.UTF8.GetString(memoryStream.ToArray());
				}
			}
		}
	}
}
