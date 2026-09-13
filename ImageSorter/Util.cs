using ImageSorter.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
            bool convertHeicToJpeg = false;
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
                    case "-heic2jpg":
                        convertHeicToJpeg = true;
                        break;
                }
            }
            var inputArgs = new Input(sourceDir, destinationDir, deleteSource, whatIf, convertHeicToJpeg);
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

        private enum CollisionKind { 
            None, //No collision, safe to copy
            Identical, //Destination already has a byte-identical copy, skip writing
            Different //Destination has a different file with the same name, disambiguate and copy
            }

        private sealed class CollisionResolution
        {
            public CollisionKind Kind;
            public string? DisambiguationSuffix;
            public string? SourceCameraModel;
            public string? ExistingCameraModel;
        }

        // Colliding files are compared read-only (size, then a full content hash) outside the
        // lock so an expensive comparison of one collision (e.g. a large video) doesn't stall
        // every other file a parallel worker is trying to copy - only the final decide-and-write
        // step below needs to be serialized.
        public static void CopyFile(List<string> movedFiles, string newDesitnationFolder, string newFullPath, string file, bool whatIf, byte[]? heicJpegBytes = null)
        {
            var resolution = ResolveCollision(file, newFullPath);

            lock (CopyFileLock)
            {
                // Two different source files can race for the same destination name - if our
                // lock-free check above found nothing but a collision has since appeared, resolve
                // it again now that we hold the lock.
                if (resolution.Kind == CollisionKind.None && File.Exists(newFullPath))
                    resolution = ResolveCollision(file, newFullPath);

                if (!whatIf && !Directory.Exists(newDesitnationFolder))
                {
                    Directory.CreateDirectory(newDesitnationFolder);
                }

                switch (resolution.Kind)
                {
                    case CollisionKind.None:
                        CopyNewFile(movedFiles, file, newFullPath, whatIf, heicJpegBytes);
                        break;
                    case CollisionKind.Identical:
                        SkipIdenticalFile(movedFiles, file, newFullPath, whatIf, heicJpegBytes);
                        break;
                    case CollisionKind.Different:
                        CopyDisambiguatedFile(movedFiles, file, newDesitnationFolder, resolution, whatIf, heicJpegBytes);
                        break;
                }
            }
        }

        private static void CopyNewFile(List<string> movedFiles, string file, string newFullPath, bool whatIf, byte[]? heicJpegBytes)
        {
            var destinationFolder = Path.GetDirectoryName(newFullPath)!;

            if (whatIf)
            {
                Print("Will move " + file + " ==> " + newFullPath);
                movedFiles.Add(file);
                CopyCompanionJsonFile(movedFiles, file, destinationFolder, disambiguatedImageFileName: null, whatIf: true);
                return;
            }

            File.Copy(file, newFullPath);
            Print("Moved " + file + " ==> " + newFullPath);
            movedFiles.Add(file);
            CopyCompanionJsonFile(movedFiles, file, destinationFolder, disambiguatedImageFileName: null, whatIf: false);
            WriteHeicJpegSibling(newFullPath, heicJpegBytes);
        }

        // The destination already holds a byte-identical copy, so there's nothing to write - but
        // the source is still safe to delete under -d, and a companion JSON the destination is
        // missing should still be picked up.
        private static void SkipIdenticalFile(List<string> movedFiles, string file, string newFullPath, bool whatIf, byte[]? heicJpegBytes)
        {
            var destinationFolder = Path.GetDirectoryName(newFullPath)!;

            Print((whatIf ? "Would skip " : "Skipped ") + file + " - identical file already exists at " + newFullPath);
            movedFiles.Add(file);
            CopyCompanionJsonFile(movedFiles, file, destinationFolder, disambiguatedImageFileName: null, whatIf);
            if (!whatIf)
                WriteHeicJpegSibling(newFullPath, heicJpegBytes);
        }

        // Same name and date, but confirmed different content - keep both by disambiguating the
        // destination filename with a hash suffix instead of routing to a separate folder.
        private static void CopyDisambiguatedFile(List<string> movedFiles, string file, string newDesitnationFolder, CollisionResolution resolution, bool whatIf, byte[]? heicJpegBytes)
        {
            var disambiguatedFileName = BuildDisambiguatedFileName(Path.GetFileName(file), resolution.DisambiguationSuffix!);
            var disambiguatedFullPath = Path.Combine(newDesitnationFolder, disambiguatedFileName);
            var cameraModelNote = " (source: " + (resolution.SourceCameraModel ?? "unknown") + ", existing: " + (resolution.ExistingCameraModel ?? "unknown") + ")";

            if (File.Exists(disambiguatedFullPath))
            {
                // Vanishingly unlikely (would need matching size plus a truncated-hash collision),
                // but don't silently overwrite or drop the file if it somehow happens.
                Print("File " + file + " not copied, a different file already exists at " + disambiguatedFullPath);
                return;
            }

            if (whatIf)
            {
                Print("Will move " + file + " ==> " + disambiguatedFullPath + " (different file with same name" + cameraModelNote + ")");
                movedFiles.Add(file);
                CopyCompanionJsonFile(movedFiles, file, newDesitnationFolder, disambiguatedFileName, whatIf: true);
                return;
            }

            File.Copy(file, disambiguatedFullPath);
            Print("Moved " + file + " ==> " + disambiguatedFullPath + " (different file with same name" + cameraModelNote + ")");
            movedFiles.Add(file);
            CopyCompanionJsonFile(movedFiles, file, newDesitnationFolder, disambiguatedFileName, whatIf: false);
            WriteHeicJpegSibling(disambiguatedFullPath, heicJpegBytes);
        }

        // Writes a JPEG copy alongside the HEIC's own resolved destination path (whatever it ended
        // up as - possibly already disambiguated). heicJpegBytes is only non-null on a real (non
        // -whatif) run where the caller already decoded the HEIC, so this never runs under -whatif
        // and never touches movedFiles - the generated JPEG has no source-side counterpart, so it
        // must stay invisible to the -d/DeleteSource cleanup that walks movedFiles.
        private static void WriteHeicJpegSibling(string heicDestinationPath, byte[]? heicJpegBytes)
        {
            if (heicJpegBytes == null)
                return;

            var jpegPath = Path.ChangeExtension(heicDestinationPath, ".jpg");

            if (!File.Exists(jpegPath))
            {
                File.WriteAllBytes(jpegPath, heicJpegBytes);
                Print("Created JPEG copy " + jpegPath);
                return;
            }

            var jpegHash = Convert.ToHexString(SHA256.HashData(heicJpegBytes)).ToLowerInvariant();
            if (string.Equals(jpegHash, ComputeFileHash(jpegPath), StringComparison.Ordinal))
            {
                Print("Skipped JPEG copy - identical file already exists at " + jpegPath);
                return;
            }

            var disambiguatedJpegPath = Path.Combine(
                Path.GetDirectoryName(jpegPath)!,
                BuildDisambiguatedFileName(Path.GetFileName(jpegPath), jpegHash[..8]));

            if (File.Exists(disambiguatedJpegPath))
            {
                Print("JPEG copy not created, a different file already exists at " + disambiguatedJpegPath);
                return;
            }

            File.WriteAllBytes(disambiguatedJpegPath, heicJpegBytes);
            Print("Created JPEG copy " + disambiguatedJpegPath + " (different file already existed at " + jpegPath + ")");
        }

        private static string BuildDisambiguatedFileName(string fileName, string disambiguationSuffix)
        {
            var extension = Path.GetExtension(fileName);
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            return baseName + "_" + disambiguationSuffix + extension;
        }

        private static CollisionResolution ResolveCollision(string file, string newFullPath)
        {
            if (!File.Exists(newFullPath))
                return new CollisionResolution { Kind = CollisionKind.None };

            string? sourceHash = null;
            var identical = false;

            if (new FileInfo(file).Length == new FileInfo(newFullPath).Length)
            {
                try
                {
                    sourceHash = ComputeFileHash(file);
                    identical = string.Equals(sourceHash, ComputeFileHash(newFullPath), StringComparison.Ordinal);
                }
                catch (IOException ex)
                {
                    LogUtility.WriteToLog("Could not verify identity of " + file + " against " + newFullPath + ", treating as a different file: " + ex.Message, LogUtility.Level.Error);
                }
            }

            if (identical)
                return new CollisionResolution { Kind = CollisionKind.Identical };

            if (sourceHash == null)
            {
                try { sourceHash = ComputeFileHash(file); }
                catch (IOException) { /* fall back to a random suffix below */ }
            }

            return new CollisionResolution
            {
                Kind = CollisionKind.Different,
                DisambiguationSuffix = sourceHash != null ? sourceHash[..8] : Guid.NewGuid().ToString("N")[..8],
                SourceCameraModel = GetCameraModel(file),
                ExistingCameraModel = GetCameraModel(newFullPath)
            };
        }

        private static string ComputeFileHash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static string? GetCameraModel(string path)
        {
            try
            {
                var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(path);
                return MetadataDateReader.SelectCameraModel(directories);
            }
            catch (Exception)
            {
                // Best-effort log context only - never let this affect the collision decision.
                return null;
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

        // Places the companion JSON next to wherever its image actually ended up. When the image
        // was disambiguated with a hash suffix (disambiguatedImageFileName set), the sidecar is
        // renamed to match so the pairing convention GetCompanionJsonFile relies on stays intact.
        private static void CopyCompanionJsonFile(List<string> movedFiles, string file, string destinationFolder, string? disambiguatedImageFileName, bool whatIf)
        {
            var companionJson = GetCompanionJsonFile(file);
            if (companionJson == null)
                return;

            var newJsonFileName = disambiguatedImageFileName == null
                ? Path.GetFileName(companionJson)
                : BuildDisambiguatedCompanionJsonName(companionJson, Path.GetFileName(file), disambiguatedImageFileName);
            var newJsonPath = Path.Combine(destinationFolder, newJsonFileName);

            if (File.Exists(newJsonPath))
            {
                Print("Companion JSON " + newJsonPath + " not copied, it already exists at the destination");
                return;
            }

            if (whatIf)
            {
                Print("Will move " + companionJson + " ==> " + newJsonPath);
                movedFiles.Add(companionJson);
                return;
            }

            File.Copy(companionJson, newJsonPath);
            Print("Moved " + companionJson + " ==> " + newJsonPath);
            movedFiles.Add(companionJson);
        }

        private static string BuildDisambiguatedCompanionJsonName(string companionJsonPath, string originalImageFileName, string disambiguatedImageFileName)
        {
            var isFullNamePattern = string.Equals(Path.GetFileName(companionJsonPath), originalImageFileName + ".json", StringComparison.OrdinalIgnoreCase);
            return isFullNamePattern
                ? disambiguatedImageFileName + ".json"
                : Path.ChangeExtension(disambiguatedImageFileName, ".json");
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

        // .json sidecar files are never sorted on their own merit - they're moved as a companion
        // of their image in CopyFile. Left in this list, they'd hit ParsePhotoDate directly and
        // get logged as an unsupported file instead of being picked up alongside their photo.
        public static List<String> GetAllFiles(String directory)
        {
            return Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(f => !string.Equals(Path.GetExtension(f), ".json", StringComparison.OrdinalIgnoreCase))
                .ToList();
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
