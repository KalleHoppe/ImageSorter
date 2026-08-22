using ImageSorter.Domain;
using Xunit;

namespace ImageSorterTests
{
    public class InputTest
    {
        [Theory]
        [InlineData("/home/user/source")]
        [InlineData("/home/user/source/")]
        public void Constructor_AbsolutePath_DoesNotThrow(string sourceDir)
        {
            var input = new Input(sourceDir, "/home/user/dest", delete: false, whatIf: false);

            Assert.Equal(sourceDir, input.SourceDir);
        }

        [Theory]
        [InlineData("relative/source")]
        [InlineData("just-a-name")]
        public void Constructor_RelativePath_ThrowsArgumentException(string relativeDir)
        {
            Assert.Throws<ArgumentException>(() => new Input(relativeDir, "/home/user/dest", delete: false, whatIf: false));
        }

        [Fact]
        public void Constructor_TrimsTrailingSeparator_FromDestinationDir()
        {
            var input = new Input("/home/user/source", "/home/user/dest/", delete: false, whatIf: false);

            Assert.Equal("/home/user/dest", input.DestinationDir);
        }
    }
}
