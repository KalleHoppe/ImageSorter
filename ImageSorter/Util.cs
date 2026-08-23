using ImageSorter.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageSorter
{
    internal class Util
    {
        // CopyFile mutates a List<string> shared across Parallel.ForEach worker threads; List<T> is
        // not thread-safe, so concurrent Add calls can also throw. Serialize access here too.
        private static readonly object CopyFileLock = new object();

        public static Input GetArgs(string[] args)
        {
            string sourceDir = args[0];
            string destinationDir = args[1];
            bool deleteSource = false;
            bool whatIf = false;
            foreach (var str in args.Where(str => !string.IsNullOrEmpty(str)))
            {
                switch (str)
                {
                    case "-d":
                        deleteSource = true;
                        break;
                    case "-whatif":
                        whatIf = true;
                        break;
                }
            }
            var inputArgs = new Input(sourceDir, destinationDir, deleteSource, whatIf);
            return inputArgs;
        }

        public static void DeleteCopiedFiles(List<string> _movedFiles, string sourceDir, bool whatIf)
        {
            Console.WriteLine("Continue and delete moved files from source dir Y/N");
            if (!String.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase)) return;

            Console.WriteLine("Are you sure? Y/N");
            if (!String.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase)) return;

            var parentDirectories = _movedFiles.Select(Path.GetDirectoryName).Distinct().ToList();

            if (whatIf)
            {
                Parallel.ForEach(_movedFiles, file => {
                    Console.WriteLine("Would be deleted: " + file);
                    LogUtility.WriteToLog("Would be deleted: " + file, LogUtility.Level.Info);
                });
                DeleteEmptyDirectoriesCascading(parentDirectories, sourceDir, whatIf: true);
                return;
            }

            Parallel.ForEach(_movedFiles, file => {
                File.Delete(file);
                Print(file + " deleted");
            });

            DeleteEmptyDirectoriesCascading(parentDirectories, sourceDir, whatIf: false);
        }

        // Walks upward from each starting directory, deleting (or, in whatIf mode, only reporting)
        // directories that are empty once every already-processed child is accounted for - so
        // /source/2020 only goes once BOTH /source/2020/01 and /source/2020/02 are gone, and
        // -whatif can cascade correctly even though nothing is actually removed from disk.
        internal static void DeleteEmptyDirectoriesCascading(IEnumerable<string?> startingDirectories, string sourceDir, bool whatIf)
        {
            var normalizedSourceDir = NormalizePath(sourceDir);
            var removed = new HashSet<string>();

            var candidates = startingDirectories
                .Where(d => !string.IsNullOrEmpty(d))
                .Select(d => NormalizePath(d!))
                .Distinct()
                .ToList();

            while (candidates.Count > 0)
            {
                var nextCandidates = new List<string>();

                foreach (var directory in candidates)
                {
                    if (directory == normalizedSourceDir || !Directory.Exists(directory))
                        continue;

                    var stillPresent = Directory.GetFileSystemEntries(directory)
                        .Select(NormalizePath)
                        .Any(entry => !removed.Contains(entry));

                    if (stillPresent)
                        continue;

                    removed.Add(directory);

                    if (whatIf)
                        Print("Would delete empty folder: " + directory);
                    else
                    {
                        Directory.Delete(directory);
                        Print("Deleted empty folder: " + directory);
                    }

                    var parent = Path.GetDirectoryName(directory);
                    if (!string.IsNullOrEmpty(parent))
                        nextCandidates.Add(NormalizePath(parent));
                }

                candidates = nextCandidates.Distinct().ToList();
            }
        }

        private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        public static void CopyFile(List<string> movedFiles, string newDesitnationFolder, string newFullPath, string duplicateDesitnationFolder, string file)
        {
            lock (CopyFileLock)
            {
                //If the dir is missing, create a new and move the file
                if (!Directory.Exists(newDesitnationFolder))
                {
                    Directory.CreateDirectory(newDesitnationFolder);
                }

                //Move the file to the corresponding directory
                if (File.Exists(newFullPath))
                {
                    newFullPath = Path.Combine(duplicateDesitnationFolder, Path.GetFileName(file));
                    if (!Directory.Exists(duplicateDesitnationFolder))
                    {
                        Directory.CreateDirectory(duplicateDesitnationFolder);
                    }
                    Print("File " + file + " already in " + newFullPath);
                    LogUtility.LogDuplicate("Moved file to " + newFullPath);
                }

                if (!File.Exists(newFullPath))
                {
                    File.Copy(file, newFullPath);
                    Print("Moved " + file + " ==> " + newFullPath);
                    movedFiles.Add(file);

                    CopyCompanionJsonFile(movedFiles, file, Path.GetDirectoryName(newFullPath)!);
                }
                else
                {
                    Print("File " + newFullPath + " not copied, It already exist in destination and duplicate folder");
                }
            }
        }

        // Sidecar metadata files (e.g. Google Takeout exports) use either the full source file
        // name plus ".json" (photo1265.jpg.json) or the base name with the extension replaced
        // (photo1265.json) - both conventions occur in practice.
        public static string? GetCompanionJsonFile(string filePath)
        {
            var fullNameJson = filePath + ".json";
            if (File.Exists(fullNameJson))
                return fullNameJson;

            var baseNameJson = Path.ChangeExtension(filePath, ".json");
            if (File.Exists(baseNameJson))
                return baseNameJson;

            return null;
        }

        // Places the companion JSON next to wherever its image actually ended up (which CopyFile
        // may have redirected to the duplicate folder), reusing the date already parsed from the
        // image's own metadata rather than re-reading the JSON - the destination is already known,
        // so there's nothing left to extract from it.
        private static void CopyCompanionJsonFile(List<string> movedFiles, string file, string destinationFolder)
        {
            var companionJson = GetCompanionJsonFile(file);
            if (companionJson == null)
                return;

            var newJsonPath = Path.Combine(destinationFolder, Path.GetFileName(companionJson));
            if (File.Exists(newJsonPath))
            {
                Print("Companion JSON " + newJsonPath + " not copied, it already exists at the destination");
                return;
            }

            File.Copy(companionJson, newJsonPath);
            Print("Moved " + companionJson + " ==> " + newJsonPath);
            movedFiles.Add(companionJson);
        }

        public static void Print(string message)
        {
            Console.WriteLine(message);
            LogUtility.WriteToLog(message, LogUtility.Level.Info);
        }



        public static string GetNewDestinationFolder(Input inputArgs, DateTime? fileDate)
        {
            if (!fileDate.HasValue)
                return inputArgs.DestinationDir;

            return Path.Combine(inputArgs.DestinationDir, fileDate.Value.ToString("yyyy"), fileDate.Value.ToString("MM"), fileDate.Value.ToString("dd"));
        }

        public static string GetDuplicateDestinationFolder(string destinationDir, DateTime? fileDate)
        {
            if (!fileDate.HasValue)
                return destinationDir;

            return Path.Combine(destinationDir, "Duplicates", fileDate.Value.ToString("yyyy"), fileDate.Value.ToString("MM"), fileDate.Value.ToString("dd"));
        }

        public static List<String> GetAllFiles(String directory)
        {
            return Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).ToList();
        }

        public static DateTime? ParsePhotoDate(string path)
        {
            IReadOnlyList<MetadataExtractor.Directory> directories;
            try
            {
                directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(path);
            }
            catch (MetadataExtractor.ImageProcessingException)
            {
                Print("UNSUPPORTED FILE not moving: " + path);
                return null;
            }
            catch (IOException)
            {
                var time = LastWriteTime(path);
                Console.WriteLine("---------------");
                Print("Corrupted File, using last Write time " + path + time);
                LogUtility.WriteToLog("Corrupted File, using last Write time " + path + time, LogUtility.Level.Error);
                Console.WriteLine("---------------");
                return time;
            }
            catch (Exception ex)
            {
                LogUtility.WriteToLog(path + "--" + ex.Message, LogUtility.Level.Error);
                Print("Unknown error: " + path);
                return null;
            }

            return MetadataDateReader.SelectDate(directories) ?? LastWriteTime(path);
        }

        public static DateTime? LastWriteTime(string path)
        {
            var info = new FileInfo(path);
            return info.LastWriteTime;
        }

    }
}
