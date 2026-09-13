using ImageSorter;
using Xunit;

namespace ImageSorterTests
{
    public class HeicJpegSiblingTest
    {
        private static string CreateTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "ImageSorterTests-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            return root;
        }

        private static byte[] FakeJpegBytes(string content) =>
            System.Text.Encoding.UTF8.GetBytes(content);

        [Fact]
        public void CopyFile_WithHeicJpegBytes_WritesBothHeicAndJpegToDestination()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);

            var heic = Path.Combine(sourceDir, "IMG_1234.heic");
            File.WriteAllText(heic, "heic bytes");

            var newFullPath = Path.Combine(destinationDir, "IMG_1234.heic");
            var movedFiles = new List<string>();
            var jpegBytes = FakeJpegBytes("jpeg bytes");

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, heic, whatIf: false, jpegBytes);

                Assert.True(File.Exists(Path.Combine(destinationDir, "IMG_1234.heic")));
                var jpegPath = Path.Combine(destinationDir, "IMG_1234.jpg");
                Assert.True(File.Exists(jpegPath));
                Assert.Equal(jpegBytes, File.ReadAllBytes(jpegPath));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_WithHeicJpegBytes_DoesNotAddGeneratedJpegToMovedFiles()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);

            var heic = Path.Combine(sourceDir, "IMG_1234.heic");
            File.WriteAllText(heic, "heic bytes");

            var newFullPath = Path.Combine(destinationDir, "IMG_1234.heic");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, heic, whatIf: false, FakeJpegBytes("jpeg bytes"));

                Assert.Single(movedFiles);
                Assert.Contains(heic, movedFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_JpegNameCollisionWithDifferentContent_Disambiguates()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);

            var heic = Path.Combine(sourceDir, "IMG_1234.heic");
            File.WriteAllText(heic, "heic bytes");

            // An unrelated JPEG already occupies the name the generated JPEG would take.
            var existingJpeg = Path.Combine(destinationDir, "IMG_1234.jpg");
            File.WriteAllText(existingJpeg, "unrelated pre-existing jpeg");

            var newFullPath = Path.Combine(destinationDir, "IMG_1234.heic");
            var movedFiles = new List<string>();
            var jpegBytes = FakeJpegBytes("newly generated jpeg");

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, heic, whatIf: false, jpegBytes);

                // The unrelated jpeg is untouched...
                Assert.Equal("unrelated pre-existing jpeg", File.ReadAllText(existingJpeg));

                // ...and the generated jpeg lands under a disambiguated name instead.
                var disambiguated = Directory.GetFiles(destinationDir, "IMG_1234_*.jpg").SingleOrDefault();
                Assert.NotNull(disambiguated);
                Assert.Equal(jpegBytes, File.ReadAllBytes(disambiguated!));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_JpegNameCollisionWithIdenticalContent_SkipsWithoutDuplicating()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);

            var heic = Path.Combine(sourceDir, "IMG_1234.heic");
            File.WriteAllText(heic, "heic bytes");

            var jpegBytes = FakeJpegBytes("already converted earlier");
            var existingJpeg = Path.Combine(destinationDir, "IMG_1234.jpg");
            File.WriteAllBytes(existingJpeg, jpegBytes);

            var newFullPath = Path.Combine(destinationDir, "IMG_1234.heic");
            var movedFiles = new List<string>();

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, heic, whatIf: false, jpegBytes);

                // Only the one pre-existing jpeg remains - no disambiguated duplicate was created.
                Assert.Single(Directory.GetFiles(destinationDir, "*.jpg"));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_WhatIf_DoesNotWriteJpeg()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);

            var heic = Path.Combine(sourceDir, "IMG_1234.heic");
            File.WriteAllText(heic, "heic bytes");

            var newFullPath = Path.Combine(destinationDir, "IMG_1234.heic");
            var movedFiles = new List<string>();

            try
            {
                // Program.cs never decodes under -whatif, so it never passes real jpeg bytes -
                // heicJpegBytes stays null here, mirroring that call site.
                Util.CopyFile(movedFiles, destinationDir, newFullPath, heic, whatIf: true, heicJpegBytes: null);

                Assert.False(Directory.Exists(destinationDir));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void CopyFile_HeicAlreadyIdenticalAtDestination_BackfillsMissingJpeg()
        {
            var root = CreateTempRoot();
            var sourceDir = Path.Combine(root, "source");
            var destinationDir = Path.Combine(root, "destination");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);

            var heic = Path.Combine(sourceDir, "IMG_1234.heic");
            File.WriteAllText(heic, "identical heic bytes");

            // The HEIC was already sorted in a previous run (before -heic2jpg was used) - no
            // jpeg sibling exists yet.
            var newFullPath = Path.Combine(destinationDir, "IMG_1234.heic");
            File.WriteAllText(newFullPath, "identical heic bytes");

            var movedFiles = new List<string>();
            var jpegBytes = FakeJpegBytes("backfilled jpeg");

            try
            {
                Util.CopyFile(movedFiles, destinationDir, newFullPath, heic, whatIf: false, jpegBytes);

                var jpegPath = Path.Combine(destinationDir, "IMG_1234.jpg");
                Assert.True(File.Exists(jpegPath));
                Assert.Equal(jpegBytes, File.ReadAllBytes(jpegPath));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
