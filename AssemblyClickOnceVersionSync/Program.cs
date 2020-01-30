using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ACOVersionSync
{
    internal class Versioner
    {
        [STAThread]
        private static void Main(string[] args)
        {
            int appRev = 0;
            string appVers = null;

            try
            {
                if (args[0] == "Debug") return;

                var csproj = args[1];
                var config = args[0];
                bool isPub = (config.StartsWith("pub", true, System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine("ARG 0:" + args[0]);
                Console.WriteLine("ARG 1:" + args[1]);

                var assInfoFile = Path.Combine(Path.GetDirectoryName(csproj), "Properties\\AssemblyInfo.cs");
                var assInfoBK = Path.Combine(Path.GetDirectoryName(csproj), "Properties\\AssemblyInfo.backup_cs");

                // backup original just in case
                if (!File.Exists(assInfoBK)) File.Copy(assInfoFile, assInfoBK);

                // decode current click once version from csproj file... 
                var lines = File.ReadAllLines(csproj);
                foreach (var item in lines)
                {
                    var txt = _getValue(item, "ApplicationRevision");
                    if (txt != null) { appRev = int.Parse(txt); continue; }

                    txt = _getValue(item, "ApplicationVersion");
                    if (txt != null)
                    {
                        appVers = txt.Substring(0, 1 + txt.LastIndexOf("."));
                        continue;
                    }
                }
                string clickOnceVersion = "\"" + appVers + appRev + "\"";
                Console.WriteLine("Click Once Version:" + clickOnceVersion);


                // overwrite assembly's versions
                Console.WriteLine("assembly info file:" + assInfoFile);
                lines = File.ReadAllLines(assInfoFile, System.Text.Encoding.UTF8);

                string desc = null;
                if (isPub)
                {
                    _initRandomDescription();
                    desc = _getRandomDescriptionTitle();
                    Console.WriteLine("PUBLISH:" + desc);
                }

                // SYNCRONIZE to CLICK ONCE
                string tmpFile = Path.GetTempFileName();
                FileInfo fi = new FileInfo(tmpFile);
                long initLen = fi.Length;
                var fileStream = fi.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                using (StreamWriter streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8))
                {
                    foreach (var item in lines)
                    {
                        var line = item;
                        if (line.StartsWith("[assembly: AssemblyVersion("))
                            line = "[assembly: AssemblyVersion(" + clickOnceVersion + ")]";
                        else if (line.StartsWith("[assembly: AssemblyFileVersion("))
                            line = "[assembly: AssemblyFileVersion(" + clickOnceVersion + ")]";
                        else if (line.StartsWith("[assembly: AssemblyCopyright("))
                            line = "[assembly: AssemblyCopyright(\"Copyright © " + DateTime.Now.Year + "\")]";
                        else if (desc != null && line.StartsWith("[assembly: AssemblyDescription("))
                            line = "[assembly: AssemblyDescription(\"" + desc + "\")]";

                        if (aTitle == null) aTitle = _getAssemblyInfoValue(line, "AssemblyTitle");
                        if (aProduct == null) aProduct = _getAssemblyInfoValue(line, "AssemblyProduct");
                        if (aDescription == null) aDescription = _getAssemblyInfoValue(line, "AssemblyDescription");
                        if (aVersion == null) aVersion = _getAssemblyInfoValue(line, "AssemblyVersion");

                        streamWriter.WriteLine(line);
                    }
                }
                fileStream.Close();

                // finally replace assInfo file
                fi = new FileInfo(tmpFile);
                long newLen = fi.Length;
                if (newLen > 0)
                {
                    File.Copy(tmpFile, assInfoFile, true);
                    Console.WriteLine("sync info file:" + assInfoFile);
                }
                if (isPub) _updateAssemblyRecord();
            }
            catch (Exception ex)
            {
                Console.WriteLine("sync failed!");
                Console.WriteLine(ex.Message);
            }

            return;
        }

        static string aTitle = null;
        static string aProduct = null;
        static string aDescription = null;
        static string aVersion = null;

        private static void _initRandomDescription()
        {
            string address = "https://www.theguardian.com/au";
            //Create the WebBrowser control
            WebBrowser wb = new WebBrowser();
            //Add a new event to process document when download is completed   
            wb.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(DisplayText);
            //Download the webpage
            wb.Url = new Uri(address);
            while (wb.ReadyState != WebBrowserReadyState.Complete)
            { Application.DoEvents(); }
        }

        private static void DisplayText(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            WebBrowser wb = (WebBrowser)sender;
            wb.Document.ExecCommand("SelectAll", false, null);
            wb.Document.ExecCommand("Copy", false, null);
            string pageTEXT = Clipboard.GetText();
            var lines = pageTEXT.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            bool start = false;
            foreach (var item in lines)
            {
                if (item.StartsWith("News, sport and opinion"))
                    start = true;
                else if (start)
                {
                    var words = item.Split(" \t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in words)
                    {
                        string str = word;
                        Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                        str = rgx.Replace(str, "");
                        if (str.Length > 4) hash.Add(str);
                    }
                }
            }

        }

        static string _getRandomDescriptionTitle()
        {
            // get 2 random 
            string str1 = hash.ElementAt(rand.Next(hash.Count));
            string str2 = hash.ElementAt(rand.Next(hash.Count));
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return (textInfo.ToTitleCase(str1) + " " + textInfo.ToTitleCase(str2));
        }


        static void _updateAssemblyRecord()
        {
            // AssemblyTitle, AssemblyProduct, AssemblyDescription, Date, AssemblyVersion
            string path = @"C:\Users\James\Documents\Visual Studio 2017\assembly_descriptions.txt";
            string record = aTitle + ", " + aProduct + ", \"" + aDescription + "\", " + DateTime.Now.ToString() + ", " + aVersion + Environment.NewLine;
            Console.WriteLine(record);
            File.AppendAllText(path, record);
        }



        static Random rand = new Random();
        static HashSet<string> hash = new HashSet<string>();
        static string _getValue(string line, string key)
        {
            var start = @"<" + key + ">";
            var end = @"</" + key + ">";
            line = line.Trim();
            if (line.StartsWith(start) && line.EndsWith(end))
            {
                line = line.Substring(start.Length);
                line = line.Substring(0, line.Length - end.Length);
                return (line);
            }
            return (null);
        }



        private static string _getAssemblyInfoValue(string line, string key)
        {
            try
            {
                var start = @"[assembly: " + key + "(\"";
                line = line.Trim();
                if (line.StartsWith(start))
                {
                    string value = line.Substring(start.Length, line.Length - 3 - start.Length);
                    return (value);
                }
            }
            catch
            {

            }
            return (null);
        }
    }
}