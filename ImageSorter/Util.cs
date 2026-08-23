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

        public static void DeleteCopiedFiles(List<string> _movedFiles, bool whatIf)
        {
            Console.WriteLine("Continue and delete moved files from source dir Y/N");
            if (!String.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase)) return;

            Console.WriteLine("Are you sure? Y/N");
            if (!String.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase)) return;
            if (whatIf)
            {
                Parallel.ForEach(_movedFiles, file => {
                    Console.WriteLine("Would be deleted: " + file);
                    LogUtility.WriteToLog("Would be deleted: " + file, LogUtility.Level.Info);
                });
                return;
            }

            Parallel.ForEach(_movedFiles, file => {
                File.Delete(file);
                Print(file + " deleted");
            });
        }

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
                }
                else
                {
                    Print("File " + newFullPath + " not copied, It already exist in destination and duplicate folder");
                }
            }
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
