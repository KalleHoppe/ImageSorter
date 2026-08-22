using ImageSorter;
using Xunit;

namespace ImageSorterTests
{
    public class DirectoryValidationTest
    {
        [Fact]
        public void ValidateDirectory_ExistingDirectory_DoesNotThrow()
        {
            Validation.ValidateDirectory(Path.GetTempPath());
        }

        [Fact]
        public void ValidateDirectory_MissingDirectory_ThrowsDirectoryNotFoundException()
        {
            var missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Assert.Throws<DirectoryNotFoundException>(() => Validation.ValidateDirectory(missingDirectory));
        }
    }
}
