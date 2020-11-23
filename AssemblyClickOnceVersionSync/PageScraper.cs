using mshtml;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace ACOVersionSync
{
    internal class PageScraper
    {
        static internal string Read(string url)
        {
            string html = string.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "C# console client";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }

            return (html);
        }
        static internal string PlainText(string html)
        {
            HTMLDocument htmldoc = new HTMLDocument();
            IHTMLDocument2 htmldoc2 = (IHTMLDocument2)htmldoc;

            Regex rRemScript = new Regex(@"<script[^>]*>[\s\S]*?</script>");
            html = rRemScript.Replace(html, "");
            rRemScript = new Regex(@"<style[^>]*>[\s\S]*?</style>");
            html = rRemScript.Replace(html, "");
            htmldoc2.write(html);
            string txt = htmldoc2.body.outerText;
            return (txt);
        }
    }
}
