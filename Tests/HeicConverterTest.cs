using System.Runtime.CompilerServices;
using ImageSorter;
using Xunit;

namespace ImageSorterTests
{
    public class HeicConverterTest
    {
        private static string GetFixturePath(string fileName, [CallerFilePath] string testFilePath = "")
            => Path.Combine(Path.GetDirectoryName(testFilePath)!, "Fixtures", fileName);

        [Theory]
        [InlineData("photo.heic", true)]
        [InlineData("photo.HEIC", true)]
        [InlineData("photo.heif", true)]
        [InlineData("photo.HEIF", true)]
        [InlineData("photo.jpg", false)]
        [InlineData("photo.mov", false)]
        public void IsHeicFile_RecognizesHeicAndHeifCaseInsensitively(string fileName, bool expected)
        {
            Assert.Equal(expected, HeicConverter.IsHeicFile(fileName));
        }

        [Fact]
        public void TryConvertToJpeg_CorruptFile_ReturnsNullWithoutThrowing()
        {
            var path = GetFixturePath("corrupt.heic");

            var result = HeicConverter.TryConvertToJpeg(path);

            Assert.Null(result);
        }
    }
}
