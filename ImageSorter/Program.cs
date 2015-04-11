using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dcraw;
using TagLib;
using File = System.IO.File;
[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace ImageSorter
{
    internal class Program
    {
        private static List<string> _movedFiles = new List<string>();
        private static string _sourceDir = "";
        private static string _destinationDir = "";
        private static bool _whatIf = false;
        private static bool _deleteSource = false;

        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Source and Destination directory missing");
                Console.WriteLine("Usage: ImageSorter.exe <source dir> <destination dir> [params](optional)");
                Console.WriteLine("Params:");
                Console.WriteLine("-whatif \t Runs the script and displays output without commiting the canges.");
                Console.WriteLine("-d \t Deletes the source files sorting and copying the files to the destination folder.");
                LogUtility.WriteToLog("No Params", LogUtility.Level.Error);
                return;
            }

            GetArgs(args);


            Validation.ValidateDirectory(_sourceDir);
            Validation.ValidateDirectory(_destinationDir);


            //Get files from source dir
            Print("----------- Image sorting started -----------");
            Print("Getting files...");
            var files = GetAllFiles(_sourceDir);
            Print("Done getting files");
            //might do an unneccecary list loop
            //LogUtility.WriteToLog("Files to move: " + files.Count(), LogUtility.Level.Info);

            //Loop thru files
            foreach (var file in files)
            {
                //For each file read date from exif
                var fileDate = ParsePhotoDate(file);
                if (!fileDate.HasValue)
                    continue;

                //Create new paths
                var newDesitnationFolder = GetNewDestinationFolder(_destinationDir, fileDate);
                var duplicateDestinationFolder = GetDuplicateDestinationFolder(_destinationDir, fileDate);
                var newFullPath = newDesitnationFolder + Path.GetFileName(file);
                Print(file + " ==> " + newFullPath);
                
                if (_whatIf)
                {
                    Print(file + " ==> " + newFullPath);
                    _movedFiles.Add(file);
                    continue;
                }

                CopyFile(newDesitnationFolder, newFullPath, duplicateDestinationFolder, file);
            }

            Print( _movedFiles.Count() + " copied to new folders");
            if(_deleteSource)
                DeleteCopiedFiles(_movedFiles, _whatIf);


            Print("----------- Image sorting finished -----------");
            Print(_movedFiles.Count() + " files have been sorted to the new " + _destinationDir);
            Print("See the log for details");
            Console.ReadLine();
        }

        private static void GetArgs(string[] args)
        {
            _sourceDir = args[0];
            _destinationDir = args[1];
            foreach (var str in args.Where(str => !string.IsNullOrEmpty(str)))
            {
                switch (str)
                {
                    case "-d":
                        _deleteSource = true;
                        break;
                    case "-whatif":
                        _whatIf = true;
                        break;
                }
            }
        }

        private static void DeleteCopiedFiles(List<string> _movedFiles, bool whatIf)
        {
            Console.WriteLine("Continue and delete moved files from source dir Y/N");
            if (Console.ReadLine() != "y" && Console.ReadLine() != "Y") return;

            Console.WriteLine("Are you sure? Y/N");
            if (Console.ReadLine() != "y" && Console.ReadLine() != "Y") return;
            if (whatIf)
            {
                foreach (var file in _movedFiles)
                {
                    Console.WriteLine("Would be deleted: " + file);
                    LogUtility.WriteToLog("Would be deleted: " + file, LogUtility.Level.Info);
                }
                return;
            }

            foreach (var file in _movedFiles)
            {
                File.Delete(file);
                Print(file + " deleted");
            }
        }

        private static void CopyFile(string newDesitnationFolder, string newFullPath, string duplicateDesitnationFolder,
            string file)
        {
            //If the dir is missing, create a new and move the file
            if (!Directory.Exists(newDesitnationFolder))
            {
                Directory.CreateDirectory(newDesitnationFolder);
            }

            //Move the file to the corresponding directory
            if (File.Exists(newFullPath))
            {
                newFullPath = duplicateDesitnationFolder + Path.GetFileName(file);
                if (!Directory.Exists(duplicateDesitnationFolder))
                {
                    Directory.CreateDirectory(duplicateDesitnationFolder);
                }
                LogUtility.LogDuplicate("Moved file to " + newFullPath);
            }

            if (!File.Exists(newFullPath))
            {
                File.Copy(file, newFullPath);
                Print("Moved " + file + " ==> " + newFullPath);
                _movedFiles.Add(file);
            }
            else
            {
                Print("File "+newFullPath+" not copied, It already exist in destination and duplicate folder");
            }
        }

        private static void Print(string message)
        {
            Console.WriteLine(message);
            LogUtility.WriteToLog(message, LogUtility.Level.Info);
        }



        private static string GetNewDestinationFolder(string destinationDir, DateTime? fileDate)
        {
            if (!fileDate.HasValue)
                return destinationDir;

            return string.Format("{0}\\{1}\\{2}\\{3}\\", destinationDir,
                    fileDate.Value.ToString("yyyy"), fileDate.Value.ToString("MM"), fileDate.Value.ToString("dd"));
        }

        private static string GetDuplicateDestinationFolder(string destinationDir, DateTime? fileDate)
        {
            if (!fileDate.HasValue)
                return destinationDir;

            return string.Format("{0}\\{1}\\{2}\\{3}\\", destinationDir + @"\Duplicates",
                    fileDate.Value.ToString("yyyy"), fileDate.Value.ToString("MM"), fileDate.Value.ToString("dd"));
        }

        public static List<String> GetAllFiles(String directory)
        {
            return Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).ToList();
        }

        private static DateTime? ParsePhotoDate(string path)
        {
            TagLib.File file = null;

            //Get CR2 EXIF date
            if (Path.GetExtension(path) == ".CR2"){
                return GetRC2PhotoDate(path);
            }

            try
            {
                file = TagLib.File.Create(path);
            }
            catch (UnsupportedFormatException)
            {
                Print("UNSUPPORTED FILE not moving: " + path);
                return null;
            }
            catch (CorruptFileException)
            {
                var time = LastWriteTime(path);
                Console.WriteLine("---------------");
                Print("Corrupted File, using last Write time " + path + time);
                LogUtility.WriteToLog("Corrupted File, using last Write time " + path + time, LogUtility.Level.Error);
                Console.WriteLine("---------------");
                return LastWriteTime(path);
            }

            var image = file as TagLib.Image.File;
            if (image == null)
            {
                Print("NOT AN IMAGE FILE  Using file date: " + path);
                return LastWriteTime(path);
            }

            return image.ImageTag.DateTime ?? LastWriteTime(path);

            //Console.WriteLine(String.Empty);
            //Console.WriteLine(path);
            //Console.WriteLine(String.Empty);

            //Console.WriteLine("Tags in object  : " + image.TagTypes);
            //Console.WriteLine(String.Empty);

            //Console.WriteLine("Comment         : " + image.ImageTag.Comment);
            //Console.Write("Keywords        : ");
            //foreach (var keyword in image.ImageTag.Keywords)
            //{
            //    Console.Write(keyword + " ");
            //}
            //Console.WriteLine();
            //Console.WriteLine("Rating          : " + image.ImageTag.Rating);
            //Console.WriteLine("DateTime        : " + image.ImageTag.DateTime);
            //Console.WriteLine("Orientation     : " + image.ImageTag.Orientation);
            //Console.WriteLine("Software        : " + image.ImageTag.Software);
            //Console.WriteLine("ExposureTime    : " + image.ImageTag.ExposureTime);
            //Console.WriteLine("FNumber         : " + image.ImageTag.FNumber);
            //Console.WriteLine("ISOSpeedRatings : " + image.ImageTag.ISOSpeedRatings);
            //Console.WriteLine("FocalLength     : " + image.ImageTag.FocalLength);
            //Console.WriteLine("FocalLength35mm : " + image.ImageTag.FocalLengthIn35mmFilm);
            //Console.WriteLine("Make            : " + image.ImageTag.Make);
            //Console.WriteLine("Model           : " + image.ImageTag.Model);

            //if (image.Properties != null)
            //{
            //    Console.WriteLine("Width           : " + image.Properties.PhotoWidth);
            //    Console.WriteLine("Height          : " + image.Properties.PhotoHeight);
            //    Console.WriteLine("Type            : " + image.Properties.Description);
            //}

            //Console.WriteLine();
            //Console.WriteLine("Writable?       : " + image.Writeable.ToString());
            //Console.WriteLine("Corrupt?        : " + image.PossiblyCorrupt.ToString());

            //if (image.PossiblyCorrupt)
            //{
            //    foreach (string reason in image.CorruptionReasons)
            //    {
            //        Console.WriteLine("    * " + reason);
            //    }
            //}

            //Console.WriteLine("---------------------------------------");
        }

        private static DateTime? GetRC2PhotoDate(string path)
        {
            DcRawState state = new DcRawState();
            state.inFilename = path;
            state.ifp = new RawStream(path);

            Identifier id = new Identifier(state);
            id.identify(state.ifp);
            return state.timestamp.HasValue ? state.timestamp.Value : LastWriteTime(path);
        }

        private static DateTime? LastWriteTime(string path)
        {
            var info = new FileInfo(path);
            return info.LastWriteTime;
        }
    }
}