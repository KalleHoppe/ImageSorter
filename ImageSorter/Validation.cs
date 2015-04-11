using System.IO;

namespace ImageSorter
{
    public static class Validation
    {
        public static void ValidateDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);
        }
    }
}