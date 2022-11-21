using dcraw;
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

        public static void Print(string message)
        {
            Console.WriteLine(message);
            LogUtility.WriteToLog(message, LogUtility.Level.Info);
        }



        public static string GetNewDestinationFolder(Input inputArgs, DateTime? fileDate)
        {
            if (!fileDate.HasValue)
                return inputArgs.DestinationDir;

            return $"{inputArgs.DestinationDir}\\{fileDate.Value.ToString("yyyy")}\\{fileDate.Value.ToString("MM")}\\{fileDate.Value.ToString("dd")}";
        }

        public static string GetDuplicateDestinationFolder(string destinationDir, DateTime? fileDate)
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

        public static DateTime? ParsePhotoDate(string path)
        {
            TagLib.File file = null;

            //Get CR2 EXIF date
            if (Path.GetExtension(path) == ".CR2")
            {
                return GetRC2PhotoDate(path);
            }

            try
            {
                file = TagLib.File.Create(path);
            }
            catch (TagLib.UnsupportedFormatException)
            {
                Print("UNSUPPORTED FILE not moving: " + path);
                return null;
            }
            catch (TagLib.CorruptFileException)
            {
                var time = LastWriteTime(path);
                Console.WriteLine("---------------");
                Print("Corrupted File, using last Write time " + path + time);
                LogUtility.WriteToLog("Corrupted File, using last Write time " + path + time, LogUtility.Level.Error);
                Console.WriteLine("---------------");
                return LastWriteTime(path);
            }
            catch (OverflowException)
            {
                var time = LastWriteTime(path);
                Console.WriteLine("---------------");
                Print("Corrupted File, using last Write time " + path + time);
                LogUtility.WriteToLog("Corrupted File, using last Write time " + path + time, LogUtility.Level.Error);
                Console.WriteLine("---------------");
                return LastWriteTime(path);
            }
            catch (Exception)
            {
                Print("Unknown error: " + path);
                return null;
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

        public static DateTime? GetRC2PhotoDate(string path)
        {
            DcRawState state = new DcRawState();
            state.inFilename = path;
            state.ifp = new RawStream(path);

            Identifier id = new Identifier(state);
            id.identify(state.ifp);
            return state.timestamp.HasValue ? state.timestamp.Value : LastWriteTime(path);
        }

        public static DateTime? LastWriteTime(string path)
        {
            var info = new FileInfo(path);
            return info.LastWriteTime;
        }

    }
}
