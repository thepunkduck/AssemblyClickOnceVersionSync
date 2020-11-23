using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ACOVersionSync
{
    public class VersionAccess
    {
        int _appRev;
        string _appVers;

        private readonly string _assInfoFile;
        private readonly string _assInfoBk;
        private readonly string _projFolder;

        Random rand = new Random();
        private HashSet<string> _hash;

        public VersionAccess(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "test") { State = true;
                    IsTest = true;
                    return;}
                if (args.Length < 1) return;
                if (args[0] == "Debug") return;

                var config = args[0];
                IsPublish = (config.StartsWith("pub", true, CultureInfo.InvariantCulture));

                var csproj = args[1];
                _projFolder = Path.GetDirectoryName(csproj);
                _assInfoFile = Path.Combine(_projFolder ?? string.Empty, "Properties\\AssemblyInfo.cs");
                _assInfoBk = Path.Combine(_projFolder ?? string.Empty, "Properties\\AssemblyInfo.backup_cs");

                ClickOnceVersion = _getClickOnceVersion(csproj);

                var lines = File.ReadAllLines(_assInfoFile, System.Text.Encoding.UTF8);
                foreach (var item in lines)
                {
                    var line = item;
                    if (AssemblyTitle == null) AssemblyTitle = _getAssemblyInfoValue(line, "AssemblyTitle");
                    if (AssemblyProduct == null) AssemblyProduct = _getAssemblyInfoValue(line, "AssemblyProduct");
                    if (AssemblyDescription == null) AssemblyDescription = _getAssemblyInfoValue(line, "AssemblyDescription");
                    if (AssemblyVersion == null) AssemblyVersion = _getAssemblyInfoValue(line, "AssemblyVersion");
                }


                State = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("sync failed!");
                Console.WriteLine(ex.Message);
            }
        }

        private string _getClickOnceVersion(string projectFile)
        {
            // decode current click once version from csproj file... 
            var lines = File.ReadAllLines(projectFile);
            foreach (var item in lines)
            {
                var txt = _getValue(item, "ApplicationRevision");
                if (txt != null) { _appRev = int.Parse(txt); continue; }
                txt = _getValue(item, "ApplicationVersion");
                if (txt != null)
                    _appVers = txt.Substring(0, 1 + txt.LastIndexOf(".", StringComparison.Ordinal));
            }

            return _appVers + _appRev;
        }

        private bool _overwriteAssemblyVersion(string description)
        {
            var lines = File.ReadAllLines(_assInfoFile, System.Text.Encoding.UTF8);

            string clickOnceVersion = "\"" + ClickOnceVersion + "\"";

            // SYNCRONIZE to CLICK ONCE
            string tmpFile = Path.GetTempFileName();
            FileInfo fi = new FileInfo(tmpFile);
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
                    else if (description != null && line.StartsWith("[assembly: AssemblyDescription("))
                    {
                        line = "[assembly: AssemblyDescription(\"" + description + "\")]";
                        AssemblyDescription = description;
                    }

                    streamWriter.WriteLine(line);
                }
            }
            fileStream.Close();

            AssemblyVersion = ClickOnceVersion;
            
            // finally replace assInfo file
            fi = new FileInfo(tmpFile);
            long newLen = fi.Length;
            if (newLen > 0)
            {
                // backup original just in case
                if (!File.Exists(_assInfoBk)) File.Copy(_assInfoFile, _assInfoBk);

                File.Copy(tmpFile, _assInfoFile, true);
                Console.WriteLine("sync info file:" + _assInfoFile);
            }

            return (true);
        }


        public string[] GetListing(int n)
        {
            if (_hash == null)
                _initRandomDescription();

            string[] listing = new string[n];
            for (int i = 0; i < n; i++)
            {
                listing[i] = _getRandomDescription();
            }

            return (listing);
        }

        // always.. 
        // title if publishing
        public bool PushClickOnceVersion()
        {
            bool state = _overwriteAssemblyVersion(null);
            return state;
        }

        public bool PublishClickOnceVersion(string title, string notes)
        {
            bool state = _overwriteAssemblyVersion(title);
            _updateAssemblyRecord();
            _updateChangeLog(notes);
            return state;
        }

        private void _updateChangeLog(string notes)
        {
            if (_projFolder == null) return;
            string path = Path.Combine(_projFolder, "ChangeLog.txt");
            string dateStr = DateTime.Now.ToString("dd/MM/yyyy");
            File.AppendAllText(path, Environment.NewLine + dateStr + "  " +
                                     ClickOnceVersion +
                                     " \"" + AssemblyDescription + "\"" + Environment.NewLine);

            if (!string.IsNullOrWhiteSpace(notes))
            {
                var indent = "            ";
                notes = notes.Replace("\r\n", "\n");
                var result = indent + notes.Replace("\n", "\r\n" + indent);
                File.AppendAllText(path, result + Environment.NewLine);
            }

        }


        // do this if publish
        private void _updateAssemblyRecord()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "assembly_descriptions.txt");
            string record = AssemblyTitle + ", " + AssemblyProduct + ", \"" + AssemblyDescription + "\", " + DateTime.Now.ToString(CultureInfo.InvariantCulture) + ", " + AssemblyVersion + Environment.NewLine;
            Console.WriteLine(record);
            File.AppendAllText(path, record);
        }


        public bool State { get; }
        public bool IsPublish { get; }
        public bool IsTest { get; }
        public string AssemblyTitle { get; }
        public string AssemblyProduct { get; }
        public string AssemblyDescription { get; private set; }
        public string AssemblyVersion { get; private set; }

        public string ClickOnceVersion { get; }

        private static string _getValue(string line, string key)
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
                // ignored
            }

            return (null);
        }

        private void _initRandomDescription()
        {
            string url = @"https://www.theguardian.com/au";
            var html = PageScraper.Read(url);
            var ptxt = PageScraper.PlainText(html);
            _hash = new HashSet<string>();
            var lines = ptxt.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            bool start = false;
            foreach (var item in lines)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;
                if (item.StartsWith("<")) continue;
                if (item.StartsWith("News, sport and opinion"))
                    start = true;
                if (start)
                {
                    var words = item.Split(" \t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in words)
                    {
                        string str = word;
                        Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                        str = rgx.Replace(str, "");
                        if (str.Any(c => char.IsDigit(c))) continue;
                        if (str.Length > 4) _hash.Add(str);
                    }
                }
            }
        }

        private string _getRandomDescription()
        {
            // get 2 random 
            string str1 = _hash.ElementAt(rand.Next(_hash.Count));
            string str2 = _hash.ElementAt(rand.Next(_hash.Count));
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return (textInfo.ToTitleCase(str1) + " " + textInfo.ToTitleCase(str2));
        }
    }
}
