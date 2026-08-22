using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ImageSorter;
using ImageSorter.Domain;
using Xunit;

namespace ImageSorterTests
{
    public class ImageSortingTest
    {
        private static string GetTestDirectory([CallerFilePath] string testFilePath = "")
            => Path.GetDirectoryName(testFilePath)!;

        [Fact]
        public void SortImages_ConcurrentSorting_DoesNotThrow()
        {
            var sourceImagesDir = Path.Combine(GetTestDirectory(), "SourceImages");
            if (!Directory.Exists(sourceImagesDir) || Directory.GetFiles(sourceImagesDir).Length == 0)
            {
                // Tests/SourceImages is a local-only, gitignored fixture folder for real photos
                // (see the failing-image regression this was set up for). Nothing to test against
                // on a fresh clone or in CI, so skip.
                return;
            }

            var destinationDir = Path.Combine(GetTestDirectory(), "Destination");
            if (Directory.Exists(destinationDir))
                Directory.Delete(destinationDir, recursive: true);
            Directory.CreateDirectory(destinationDir);

            // Sorting a real photo library processes many files concurrently (Program.cs uses
            // Parallel.ForEach). A single source image can't reproduce a concurrency bug, so
            // duplicate the real fixture files to give Parallel.ForEach enough concurrent work.
            var tempSourceDir = Path.Combine(Path.GetTempPath(), "ImageSorterTests-" + Guid.NewGuid());
            Directory.CreateDirectory(tempSourceDir);
            try
            {
                var realFiles = Directory.GetFiles(sourceImagesDir);
                for (var copy = 0; copy < 800; copy++)
                {
                    foreach (var realFile in realFiles)
                    {
                        var copyPath = Path.Combine(tempSourceDir, $"{copy}_{Path.GetFileName(realFile)}");
                        File.Copy(realFile, copyPath);
                    }
                }

                var inputArgs = new Input(tempSourceDir, destinationDir, delete: false, whatIf: false);
                var files = Util.GetAllFiles(inputArgs.SourceDir);
                var movedFiles = new List<string>();
                var exceptions = new ConcurrentBag<Exception>();
                // ParsePhotoDate catches its own exceptions and returns null instead of throwing
                // (see Util.cs), so under the race it doesn't crash the process - it silently
                // fails to parse the date and Program.cs would skip the file entirely. That's the
                // actual bug the user hit ("error during runtime when sorting images"), so track
                // it explicitly rather than relying on an exception to escape.
                var filesWithoutDate = new ConcurrentBag<string>();

                Parallel.ForEach(files, file =>
                {
                    try
                    {
                        var fileDate = Util.ParsePhotoDate(file);
                        if (!fileDate.HasValue)
                        {
                            filesWithoutDate.Add(file);
                            return;
                        }

                        var newDestinationFolder = Util.GetNewDestinationFolder(inputArgs, fileDate);
                        var duplicateDestinationFolder = Util.GetDuplicateDestinationFolder(inputArgs.DestinationDir, fileDate);
                        var newFullPath = Path.Combine(newDestinationFolder, Path.GetFileName(file));

                        Util.CopyFile(movedFiles, newDestinationFolder, newFullPath, duplicateDestinationFolder, file);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });

                Assert.Empty(exceptions);
                Assert.Empty(filesWithoutDate);
                Assert.Equal(files.Count, movedFiles.Count);
            }
            finally
            {
                Directory.Delete(tempSourceDir, recursive: true);
            }
        }
    }
}
