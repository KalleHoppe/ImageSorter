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
            var image = Path.Combine(root, "photo.jpg");
            var json = Path.Combine(root, "photo.jpg.json");
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
            var image = Path.Combine(root, "photo.jpg");
            var json = Path.Combine(root, "photo.json");
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
            var image = Path.Combine(root, "photo.jpg");
            var fullNameJson = Path.Combine(root, "photo.jpg.json");
            var baseNameJson = Path.Combine(root, "photo.json");
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
            var image = Path.Combine(root, "photo.jpg");
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
        public void CopyFile_WithCompanionJson_CopiesBothToSameDestination()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            var duplicateDir = Path.Combine(root, "duplicate");
            Directory.CreateDirectory(sourceDir);

            var image = Path.Combine(sourceDir, "photo.jpg");
            var json = Path.Combine(sourceDir, "photo.jpg.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{}");

            var newFullPath = Path.Combine(destinationDir, "photo.jpg");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, duplicateDir, image);

                Assert.True(File.Exists(Path.Combine(destinationDir, "photo.jpg")));
                Assert.True(File.Exists(Path.Combine(destinationDir, "photo.jpg.json")));
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
            var duplicateDir = Path.Combine(root, "duplicate");
            Directory.CreateDirectory(sourceDir);

            var image = Path.Combine(sourceDir, "photo.jpg");
            File.WriteAllText(image, "image bytes");

            var newFullPath = Path.Combine(destinationDir, "photo.jpg");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, duplicateDir, image);

                Assert.True(File.Exists(Path.Combine(destinationDir, "photo.jpg")));
                Assert.Single(movedFiles);
                Assert.Contains(image, movedFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_ImageRedirectedToDuplicateFolder_CompanionJsonFollowsIt()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            var duplicateDir = Path.Combine(root, "duplicate");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);

            var image = Path.Combine(sourceDir, "photo.jpg");
            var json = Path.Combine(sourceDir, "photo.jpg.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{}");

            // Pre-occupy the primary destination so CopyFile redirects this file to duplicateDir.
            var newFullPath = Path.Combine(destinationDir, "photo.jpg");
            File.WriteAllText(newFullPath, "a different photo already there");

            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, duplicateDir, image);

                Assert.False(File.Exists(Path.Combine(destinationDir, "photo.jpg.json")));
                Assert.True(File.Exists(Path.Combine(duplicateDir, "photo.jpg")));
                Assert.True(File.Exists(Path.Combine(duplicateDir, "photo.jpg.json")));
                Assert.Contains(json, movedFiles);
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
            var duplicateDir = Path.Combine(root, "duplicate");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);

            var image = Path.Combine(sourceDir, "photo.jpg");
            var json = Path.Combine(sourceDir, "photo.jpg.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{\"source\":true}");

            var existingDestinationJson = Path.Combine(destinationDir, "photo.jpg.json");
            File.WriteAllText(existingDestinationJson, "{\"source\":false}");

            var newFullPath = Path.Combine(destinationDir, "photo.jpg");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, duplicateDir, image);

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
