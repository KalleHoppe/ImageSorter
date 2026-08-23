using ImageSorter;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Xunit;
using Directory = MetadataExtractor.Directory;

namespace ImageSorterTests
{
    public class MetadataDateReaderTest
    {
        [Fact]
        public void SelectDate_NoDirectories_ReturnsNull()
        {
            var result = MetadataDateReader.SelectDate(new List<Directory>());

            Assert.Null(result);
        }

        [Fact]
        public void SelectDate_NoRelevantTags_ReturnsNull()
        {
            var subIfd = new ExifSubIfdDirectory();

            var result = MetadataDateReader.SelectDate(new List<Directory> { subIfd });

            Assert.Null(result);
        }

        [Fact]
        public void SelectDate_OnlyDateTimeOriginal_ReturnsIt()
        {
            var expected = new DateTime(2024, 3, 15, 8, 30, 0);
            var subIfd = new ExifSubIfdDirectory();
            subIfd.Set(ExifDirectoryBase.TagDateTimeOriginal, expected);

            var result = MetadataDateReader.SelectDate(new List<Directory> { subIfd });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SelectDate_DateTimeOriginalAndDigitized_PrefersOriginal()
        {
            var original = new DateTime(2024, 3, 15, 8, 30, 0);
            var digitized = new DateTime(2024, 3, 16, 9, 0, 0);
            var subIfd = new ExifSubIfdDirectory();
            subIfd.Set(ExifDirectoryBase.TagDateTimeOriginal, original);
            subIfd.Set(ExifDirectoryBase.TagDateTimeDigitized, digitized);

            var result = MetadataDateReader.SelectDate(new List<Directory> { subIfd });

            Assert.Equal(original, result);
        }

        [Fact]
        public void SelectDate_OnlyDigitized_FallsBackToDigitized()
        {
            var digitized = new DateTime(2024, 3, 16, 9, 0, 0);
            var subIfd = new ExifSubIfdDirectory();
            subIfd.Set(ExifDirectoryBase.TagDateTimeDigitized, digitized);

            var result = MetadataDateReader.SelectDate(new List<Directory> { subIfd });

            Assert.Equal(digitized, result);
        }

        [Fact]
        public void SelectDate_DigitizedAndIfd0DateTime_PrefersDigitized()
        {
            var digitized = new DateTime(2024, 3, 16, 9, 0, 0);
            var modified = new DateTime(2024, 3, 20, 12, 0, 0);
            var subIfd = new ExifSubIfdDirectory();
            subIfd.Set(ExifDirectoryBase.TagDateTimeDigitized, digitized);
            var ifd0 = new ExifIfd0Directory();
            ifd0.Set(ExifDirectoryBase.TagDateTime, modified);

            var result = MetadataDateReader.SelectDate(new List<Directory> { subIfd, ifd0 });

            Assert.Equal(digitized, result);
        }

        [Fact]
        public void SelectDate_OnlyIfd0DateTime_FallsBackToIt()
        {
            var modified = new DateTime(2024, 3, 20, 12, 0, 0);
            var ifd0 = new ExifIfd0Directory();
            ifd0.Set(ExifDirectoryBase.TagDateTime, modified);

            var result = MetadataDateReader.SelectDate(new List<Directory> { ifd0 });

            Assert.Equal(modified, result);
        }
    }
}
