using ImageSorter;
using Xunit;

namespace ImageSorterTests
{
    public class CompanionJsonFileTest
    {
        private static string CreateTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "ImageSorterTests-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            return root;
        }

        [Fact]
        public void GetCompanionJsonFile_FullNamePattern_ReturnsIt()
        {
            var root = CreateTempRoot();
            var image = Path.Combine(root, "photo1265.jpg");
            var json = Path.Combine(root, "photo1265.jpg.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{}");

            try
            {
                var result = Util.GetCompanionJsonFile(image);

                Assert.Equal(json, result);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void GetCompanionJsonFile_BaseNamePattern_ReturnsIt()
        {
            var root = CreateTempRoot();
            var image = Path.Combine(root, "photo1265.jpg");
            var json = Path.Combine(root, "photo1265.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{}");

            try
            {
                var result = Util.GetCompanionJsonFile(image);

                Assert.Equal(json, result);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void GetCompanionJsonFile_BothPatternsExist_PrefersFullNamePattern()
        {
            var root = CreateTempRoot();
            var image = Path.Combine(root, "photo1265.jpg");
            var fullNameJson = Path.Combine(root, "photo1265.jpg.json");
            var baseNameJson = Path.Combine(root, "photo1265.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(fullNameJson, "{}");
            File.WriteAllText(baseNameJson, "{}");

            try
            {
                var result = Util.GetCompanionJsonFile(image);

                Assert.Equal(fullNameJson, result);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void GetCompanionJsonFile_NoCompanion_ReturnsNull()
        {
            var root = CreateTempRoot();
            var image = Path.Combine(root, "photo1265.jpg");
            File.WriteAllText(image, "image bytes");

            try
            {
                var result = Util.GetCompanionJsonFile(image);

                Assert.Null(result);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_WithFullNameCompanionJson_CopiesBothToSameDestination()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);

            var image = Path.Combine(sourceDir, "photo1265.jpg");
            var json = Path.Combine(sourceDir, "photo1265.jpg.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{}");

            var newFullPath = Path.Combine(destinationDir, "photo1265.jpg");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, image, whatIf: false);

                Assert.True(File.Exists(Path.Combine(destinationDir, "photo1265.jpg")));
                Assert.True(File.Exists(Path.Combine(destinationDir, "photo1265.jpg.json")));
                Assert.Contains(image, movedFiles);
                Assert.Contains(json, movedFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_WithBaseNameCompanionJson_CopiesBothToSameDestination()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);

            var image = Path.Combine(sourceDir, "photo1265.jpg");
            var json = Path.Combine(sourceDir, "photo1265.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{}");

            var newFullPath = Path.Combine(destinationDir, "photo1265.jpg");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, image, whatIf: false);

                Assert.True(File.Exists(Path.Combine(destinationDir, "photo1265.jpg")));
                Assert.True(File.Exists(Path.Combine(destinationDir, "photo1265.json")));
                Assert.Contains(image, movedFiles);
                Assert.Contains(json, movedFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_WithoutCompanionJson_OnlyCopiesImage()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);

            var image = Path.Combine(sourceDir, "photo1265.jpg");
            File.WriteAllText(image, "image bytes");

            var newFullPath = Path.Combine(destinationDir, "photo1265.jpg");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, image, whatIf: false);

                Assert.True(File.Exists(Path.Combine(destinationDir, "photo1265.jpg")));
                Assert.Single(movedFiles);
                Assert.Contains(image, movedFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_SameNameDifferentContent_CopiesWithHashSuffixAndRenamesCompanionJson()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);

            var image = Path.Combine(sourceDir, "photo1265.jpg");
            var json = Path.Combine(sourceDir, "photo1265.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{}");

            // A different file already occupies the destination name, so the size/hash check
            // must find a mismatch and disambiguate rather than overwrite or skip.
            var newFullPath = Path.Combine(destinationDir, "photo1265.jpg");
            File.WriteAllText(newFullPath, "a different photo already there, different length");

            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, image, whatIf: false);

                var disambiguated = Directory.GetFiles(destinationDir, "photo1265_*.jpg").SingleOrDefault();
                Assert.NotNull(disambiguated);
                Assert.Equal("image bytes", File.ReadAllText(disambiguated!));

                var disambiguatedJson = Path.ChangeExtension(disambiguated, ".json");
                Assert.True(File.Exists(disambiguatedJson));

                Assert.Contains(image, movedFiles);
                Assert.Contains(json, movedFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_SameNameIdenticalContent_SkipsCopyButStillTracksSourceForDeletion()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);

            var image = Path.Combine(sourceDir, "photo1265.jpg");
            File.WriteAllText(image, "identical bytes");

            var newFullPath = Path.Combine(destinationDir, "photo1265.jpg");
            File.WriteAllText(newFullPath, "identical bytes");
            var originalWriteTime = File.GetLastWriteTimeUtc(newFullPath);

            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, image, whatIf: false);

                // Only the pre-existing destination file remains - nothing else was written there.
                Assert.Single(Directory.GetFiles(destinationDir));
                Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(newFullPath));
                Assert.Contains(image, movedFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_CompanionJsonAlreadyAtDestination_IsNotOverwrittenOrTrackedForDeletion()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);

            var image = Path.Combine(sourceDir, "photo1265.jpg");
            var json = Path.Combine(sourceDir, "photo1265.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{\"source\":true}");

            var existingDestinationJson = Path.Combine(destinationDir, "photo1265.json");
            File.WriteAllText(existingDestinationJson, "{\"source\":false}");

            var newFullPath = Path.Combine(destinationDir, "photo1265.jpg");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, image, whatIf: false);

                Assert.Equal("{\"source\":false}", File.ReadAllText(existingDestinationJson));
                Assert.DoesNotContain(json, movedFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
