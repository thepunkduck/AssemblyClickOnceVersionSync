using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
                if (args.Length == 1 && args[0] == "test")
                {
                    State = true;
                    IsTest = true;
                    return;
                }
                Console.WriteLine("VersionAccess");

                if (args.Length < 1) return;
                if (args[0] == "Debug") return;

                Console.WriteLine("ARG0: " + args[0]);
                Console.WriteLine("ARG1: " + args[1]);
                IsPublish = args[0].ToLower().Contains("publish");
                Console.WriteLine("publish?: " + IsPublish);
                var csproj = args[1];
                Console.WriteLine("proj: " + csproj);
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
                Console.WriteLine("***** " + ex.Message);
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
                Console.WriteLine("sync info file: " + _assInfoFile);
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

            _sanitizeChangeLog(path);

            // Prepend new text
            string dateStr = DateTime.Now.ToString("dd/MM/yyyy");
            var entry = dateStr + "  " + ClickOnceVersion + " \"" + AssemblyDescription + "\"" + Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                var indent = "            ";
                notes = notes.Replace("\r\n", "\n");
                var result = indent + notes.Replace("\n", "\r\n" + indent);
                entry += (result + Environment.NewLine);
            }

            string existingContent = File.ReadAllText(path);
            string updatedContent = entry + Environment.NewLine + existingContent;

            // Write updated content back to the file
            File.WriteAllText(path, updatedContent);
        }

        class ChangeItem
        {
            public DateTime Date;
            public string Header;
            public string VersionNum;
            public string VersionName;
            public List<string> Lines = new List<string>();
            public DateTime SortableDateTime;
            public ChangeItem(string line, DateTime date, string versionNum, string versionName)
            {
                Header = line;
                Date = date;
                VersionNum = versionNum;
                VersionName = versionName;

                try
                {
                    var tmp = VersionNum.Replace(".", "");
                    var res = int.Parse(tmp);
                    SortableDateTime = Date.AddMilliseconds(res);
                }
                catch
                {
                    SortableDateTime = Date;
                }
            }




            internal void AddLine(string line)
            {
                if (line == null) return;
                Lines.Add(line);
            }

            internal void TrimLines()
            {
                if (Lines.Count == 0) return;

                bool trimmed = true;
                while (trimmed)
                {
                    trimmed = false;
                    var line = Lines.FirstOrDefault();
                    if (line != null && string.IsNullOrWhiteSpace(line))
                    {
                        Lines.RemoveAt(0);
                        trimmed = true;
                    }
                    line = Lines.LastOrDefault();
                    if (line != null && string.IsNullOrWhiteSpace(line))
                    {
                        Lines.RemoveAt(Lines.Count - 1);
                        trimmed = true;
                    }
                }
            }
        }
        private void _sanitizeChangeLog(string path)
        {
            try
            {
                // read file
                var lines = File.ReadAllLines(path);

                List<ChangeItem> changes = new List<ChangeItem>();
                ChangeItem ci = null;
                // split by date lines
                foreach (var line in lines)
                {
                    if (_isHeader(line, out DateTime date, out string versionNum, out string versionName))
                    {
                        if (string.IsNullOrWhiteSpace(versionName)) 
                            versionName = _getRandomDescription();
                        ci = new ChangeItem(line, date, versionNum, versionName);
                        changes.Add(ci);
                    }
                    else if (ci != null)
                    {
                        ci.AddLine(line);
                    }
                }


                foreach (var c in changes) c.TrimLines();
                // sort by date 
                changes = changes.OrderByDescending(c => c.SortableDateTime).ToList();

                // sanitize?
                // if version and name same, merge them
                var res = changes.GroupBy(c => c.VersionNum + c.VersionName);
                
                foreach (IGrouping<string, ChangeItem> item in res)
                {
                    ChangeItem c0 = item.Last();
                    // merge others in
                    var rev = item.Reverse();
                    foreach (ChangeItem c in rev)
                    {
                        if (c0 == c) continue;
                        foreach(var line in c.Lines) 
                            c0.AddLine(line);
                        c.Header = null; // remove!
                    }
                }

                changes = changes.Where(c => c.Header != null).ToList();
                // output with latest changes at top
                // spaces between

                StringBuilder sb = new StringBuilder();
                foreach (var c in changes)
                {
                    string dateStr = c.Date.ToString("dd/MM/yyyy");
                    var entry = dateStr + "  " + c.VersionNum + " \"" + c.VersionName + "\"";
                    sb.AppendLine(entry);
                    foreach (var line in c.Lines)
                        sb.AppendLine(line);
                    sb.AppendLine();
                }

                File.WriteAllText(path, sb.ToString());
            }
            catch
            {

            }

        }

        private static bool _isHeader(string line, out DateTime date, out string versionNum, out string versionName)
        {
            date = DateTime.MinValue;
            versionName = string.Empty;
            versionNum = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(line)) return false;
                if (line.Length < 12) return false;
                if (line[2] != '/') return false;
                string[] formats = { "dd/MM/yyyy" };
                var tmp = line.Substring(0, 10).Trim();
                DateTime.TryParseExact(tmp, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                date = result.Date;

                tmp = line.Substring(10).Trim();
                var idx = tmp.IndexOf("\"");
                versionNum = tmp.Substring(0, idx).Trim();

                versionName = tmp.Substring(idx).Trim();
                versionName = versionName.Trim("\"".ToCharArray());
                return true;
            }
            catch
            {
                return false;
            }

        }


        // do this if publish
        private void _updateAssemblyRecord()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "assembly_descriptions.txt");

            string record = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            record += Environment.NewLine;
            record += $"AssemblyTitle:{AssemblyTitle}  AssemblyProduct:{AssemblyProduct}  AssemblyDescription:{AssemblyDescription}";
            record += Environment.NewLine;
            record += $"AssemblyVersion:{AssemblyVersion}";
            record += Environment.NewLine;

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
