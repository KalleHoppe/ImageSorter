using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageSorter.Domain
{
    internal class Input
    {
        public Input(string sourceDir, string destinationDir, bool delete, bool whatIf, bool convertHeicToJpeg)
        {
            if (string.IsNullOrWhiteSpace(sourceDir))
                throw new ArgumentNullException(nameof(sourceDir));
            if (string.IsNullOrWhiteSpace(destinationDir))
                throw new ArgumentNullException(nameof(destinationDir));

            if (!IsValidAbsolutePath(sourceDir))
                throw new ArgumentException("Invalid path format, please check the source dir format");

            if (!IsValidAbsolutePath(destinationDir))
                throw new ArgumentException("Invalid path format, please check the destination dir format");

            SourceDir = sourceDir;
            DestinationDir = destinationDir.TrimEnd('\\', '/');
            DeleteSource = delete;
            WhatIf = whatIf;
            ConvertHeicToJpeg = convertHeicToJpeg;
        }

        private static bool IsValidAbsolutePath(string path)
        {
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return false;

            return Path.IsPathRooted(path);
        }



        public string SourceDir { get; }
        public string DestinationDir { get; }
        public bool DeleteSource { get; }
        public bool WhatIf { get; }
        public bool ConvertHeicToJpeg { get; }
    }
}
