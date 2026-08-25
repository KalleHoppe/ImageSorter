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

        // Not used to decide whether two files are the same - a content hash already answers
        // that definitively. This is only surfaced as human-readable context in the log when a
        // hash mismatch confirms two same-named files are genuinely different.
        public static string? SelectCameraModel(IReadOnlyList<Directory> directories)
        {
            foreach (var directory in directories.OfType<ExifIfd0Directory>())
            {
                var model = directory.GetDescription(ExifDirectoryBase.TagModel);
                if (!string.IsNullOrWhiteSpace(model))
                    return model;
            }

            return null;
        }
    }
}
