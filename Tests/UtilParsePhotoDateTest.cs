using System.Runtime.CompilerServices;
using ImageSorter;
using Xunit;

namespace ImageSorterTests
{
    public class UtilParsePhotoDateTest
    {
        private static string GetFixturePath(string fileName, [CallerFilePath] string testFilePath = "")
            => Path.Combine(Path.GetDirectoryName(testFilePath)!, "Fixtures", fileName);

        [Fact]
        public void ParsePhotoDate_JpegWithExifDateTimeOriginal_ReturnsThatDate()
        {
            var path = GetFixturePath("minimal-exif.jpg");

            var result = Util.ParsePhotoDate(path);

            Assert.Equal(new DateTime(2024, 3, 15, 8, 30, 0), result);
        }

        [Fact]
        public void ParsePhotoDate_QuickTimeMovWithCreatedDate_ReturnsThatDate()
        {
            var path = GetFixturePath("minimal-quicktime.mov");

            var result = Util.ParsePhotoDate(path);

            Assert.Equal(new DateTime(2024, 6, 1, 14, 45, 0), result);
        }

        [Fact]
        public void ParsePhotoDate_Cr2RawWithExifDateTimeOriginal_ReturnsThatDate()
        {
            var path = GetFixturePath("minimal-raw.cr2");

            var result = Util.ParsePhotoDate(path);

            Assert.Equal(new DateTime(2024, 9, 10, 7, 15, 30), result);
        }

        [Fact]
        public void ParsePhotoDate_UnsupportedFormat_ReturnsNull()
        {
            var path = GetFixturePath("unsupported.txt");

            var result = Util.ParsePhotoDate(path);

            Assert.Null(result);
        }

        [Fact]
        public void ParsePhotoDate_CorruptFile_FallsBackToLastWriteTime()
        {
            var path = GetFixturePath("corrupt.jpg");

            var result = Util.ParsePhotoDate(path);

            Assert.Equal(new FileInfo(path).LastWriteTime, result);
        }
    }
}
