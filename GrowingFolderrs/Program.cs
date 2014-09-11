using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GrowingFolderrs
{
    class Program
    {
        private const string ScanResultFilename = "growing-folders-scan-result.xml";

        static void Main(string[] args)
        {
            string folder = @"c:\";
            var scanFolderResult = ScanFolder(folder);
            var previousFolderScan = ReadPreviousFolderScan(folder);
            if (previousFolderScan != null)
            {
                var folderDiffs = CompareFolders(scanFolderResult, previousFolderScan);
                foreach (var diff in folderDiffs.Where(d => d.Diff > 0).OrderByDescending(d => d.Diff).Take(100))
                {
                    Console.WriteLine(diff.Diff + " " + diff.Folder);
                }
                Console.WriteLine("Update previous state?");
                if (Console.ReadKey().KeyChar == 'y')
                {
                    WritePreviousFolderScan(scanFolderResult);
                }
            }
            else
            {
                Console.WriteLine("First time");
                WritePreviousFolderScan(scanFolderResult);
            }
            Console.ReadKey();
        }

        private static IEnumerable<FolderDiff> CompareFolders(ScanFolderResult scanFolderResult, ScanFolderResult previousFolderScan)
        {
            var previousFolders = GetAllFolders(previousFolderScan).ToDictionary(f => f.Folder, f => f.FolderSize);
            foreach (var folder in GetAllFolders(scanFolderResult))
            {
                var diff = folder.FolderSize;
                if (previousFolders.ContainsKey(folder.Folder))
                {
                    diff = folder.FolderSize - previousFolders[folder.Folder];
                }
                yield return new FolderDiff{ Folder = folder.Folder, Diff = diff };
            }
        }

        private static IEnumerable<ScanFolderResult> GetAllFolders(ScanFolderResult scanFolderResult)
        {
            yield return scanFolderResult;
            foreach (var subFolder in scanFolderResult.SubFolders.SelectMany(GetAllFolders))
            {
                yield return subFolder;
            }
        }

        private static void WritePreviousFolderScan(ScanFolderResult scanFolderResult)
        {
            var path = Path.Combine(scanFolderResult.Folder, ScanResultFilename);
            new XmlSerializer(typeof(ScanFolderResult)).Serialize(File.Create(path), scanFolderResult);
        }

        private static ScanFolderResult ReadPreviousFolderScan(string folder)
        {
            var path = Path.Combine(folder, ScanResultFilename);
            if (File.Exists(path))
            {
                using (var fileStream = File.OpenRead(path))
                {
                    return new XmlSerializer(typeof (ScanFolderResult)).Deserialize(fileStream) as ScanFolderResult;
                }
            }
            return null;
        }

        private static ScanFolderResult ScanFolder(string folder, bool log = true)
        {
            var res = new ScanFolderResult(folder);
            try
            {
                IEnumerable<string> subFolders = Directory.GetDirectories(folder);
                Parallel.ForEach(subFolders, subFolder =>
                {
                    if (log)
                    {
                        Console.WriteLine(subFolder);
                    }
                    var subFolderResult = ScanFolder(subFolder, false);
                    lock (res)
                    {
                        res.AddSubFolder(subFolderResult);
                    }
                });
                res.FolderSize = GetFolderSize(folder);
            }
            catch (UnauthorizedAccessException e)
            {
                
            }
            return res;
        }

        private static long GetFolderSize(string folder)
        {
            var dirInfo = new DirectoryInfo(folder);
            return dirInfo.GetFiles().Sum(f => f.Length);
        }
    }

    internal class FolderDiff
    {
        public string Folder { get; set; }
        public long Diff { get; set; }
    }

    public class ScanFolderResult
    {
        public string Folder { get; set; }
        public long FolderSize { get; set; }
        public List<ScanFolderResult> SubFolders = new List<ScanFolderResult>();

        public ScanFolderResult()
        {
            
        }

        public ScanFolderResult(string folder)
        {
            Folder = folder;
        }

        public void AddSubFolder(ScanFolderResult subFolderResult)
        {
            SubFolders.Add(subFolderResult);
        }
    }
}
