using Openize.Heic.Decoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageSorter
{
    internal static class HeicConverter
    {
        private const int JpegQuality = 90;
        private static readonly string[] HeicExtensions = { ".heic", ".heif" };

        public static bool IsHeicFile(string filePath) =>
            HeicExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

        // Decodes and re-encodes entirely in memory. Returns null (after logging a warning) on any
        // failure - e.g. a corrupt file or a HEIC variant the decoder doesn't support - so the
        // caller can fall back to moving the original HEIC untouched, the same way
        // Util.ParsePhotoDate degrades gracefully on unsupported/corrupt files.
        public static byte[]? TryConvertToJpeg(string heicFilePath)
        {
            try
            {
                using var input = File.OpenRead(heicFilePath);
                var heicImage = HeicImage.Load(input);
                var pixels = heicImage.GetByteArray(PixelFormat.Bgra32);

                using var image = Image.LoadPixelData<Bgra32>(pixels, (int)heicImage.Width, (int)heicImage.Height);
                using var output = new MemoryStream();
                image.SaveAsJpeg(output, new JpegEncoder { Quality = JpegQuality });
                return output.ToArray();
            }
            catch (Exception ex)
            {
                LogUtility.WriteToLog("HEIC to JPEG conversion failed for " + heicFilePath + ": " + ex.Message, LogUtility.Level.Warn);
                return null;
            }
        }
    }
}
