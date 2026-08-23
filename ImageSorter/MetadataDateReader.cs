using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using Directory = MetadataExtractor.Directory;

namespace ImageSorter
{
    internal static class MetadataDateReader
    {
        public static DateTime? SelectDate(IReadOnlyList<Directory> directories)
        {
            return TryTag<ExifSubIfdDirectory>(directories, ExifDirectoryBase.TagDateTimeOriginal)
                ?? TryTag<ExifSubIfdDirectory>(directories, ExifDirectoryBase.TagDateTimeDigitized)
                ?? TryTag<ExifIfd0Directory>(directories, ExifDirectoryBase.TagDateTime)
                ?? TryTag<QuickTimeMovieHeaderDirectory>(directories, QuickTimeMovieHeaderDirectory.TagCreated);
        }

        private static DateTime? TryTag<TDirectory>(IReadOnlyList<Directory> directories, int tagType)
            where TDirectory : Directory
        {
            foreach (var directory in directories.OfType<TDirectory>())
            {
                if (directory.TryGetDateTime(tagType, out var value))
                    return value;
            }

            return null;
        }
    }
}
