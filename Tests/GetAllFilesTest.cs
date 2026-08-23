using ImageSorter;
using Xunit;

namespace ImageSorterTests
{
    public class GetAllFilesTest
    {
        private static string CreateTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "ImageSorterTests-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            return root;
        }

        [Fact]
        public void GetAllFiles_ExcludesJsonSidecarFiles()
        {
            var root = CreateTempRoot();
            var image = Path.Combine(root, "photo1265.jpg");
            var json = Path.Combine(root, "photo1265.jpg.json");
            File.WriteAllText(image, "image bytes");
            File.WriteAllText(json, "{}");

            try
            {
                var result = Util.GetAllFiles(root);

                Assert.Contains(image, result);
                Assert.DoesNotContain(json, result);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void GetAllFiles_ExcludesJsonRegardlessOfCase()
        {
            var root = CreateTempRoot();
            var json = Path.Combine(root, "photo1265.JSON");
            File.WriteAllText(json, "{}");

            try
            {
                var result = Util.GetAllFiles(root);

                Assert.Empty(result);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void GetAllFiles_IncludesNonJsonFilesFromNestedFolders()
        {
            var root = CreateTempRoot();
            var subDir = Path.Combine(root, "2020", "01");
            Directory.CreateDirectory(subDir);

            var jpg = Path.Combine(root, "photo1.jpg");
            var mov = Path.Combine(subDir, "clip.mov");
            var cr2 = Path.Combine(subDir, "raw.cr2");
            var json = Path.Combine(subDir, "clip.json");
            File.WriteAllText(jpg, "a");
            File.WriteAllText(mov, "b");
            File.WriteAllText(cr2, "c");
            File.WriteAllText(json, "{}");

            try
            {
                var result = Util.GetAllFiles(root);

                Assert.Contains(jpg, result);
                Assert.Contains(mov, result);
                Assert.Contains(cr2, result);
                Assert.DoesNotContain(json, result);
                Assert.Equal(3, result.Count);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
