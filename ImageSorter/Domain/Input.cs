using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImageSorter.Domain
{
    internal class Input
    {
        public Input(string sourceDir, string destinationDir, bool delete, bool whatIf)
        {
            if (string.IsNullOrWhiteSpace(sourceDir))
                throw new ArgumentNullException(nameof(sourceDir));
            if (string.IsNullOrWhiteSpace(destinationDir))
                throw new ArgumentNullException(nameof(destinationDir));
            var dirRegex = @"^[a-zA-Z]:\\(?:\w+\\?)*$";
            var sourceMatch = Regex.Match(sourceDir, dirRegex, RegexOptions.IgnoreCase);
            
            if (!sourceMatch.Success)
                throw new ArgumentException("Invalid path format, please check the source dir format");

            var destmatch = Regex.Match(destinationDir, dirRegex, RegexOptions.IgnoreCase);
            if (!destmatch.Success)
                throw new ArgumentException("Invalid path format, please check the destination dir format");

            SourceDir = sourceDir;
            DestinationDir = destinationDir.TrimEnd('\\');
            DeleteSource = delete;
            WhatIf = whatIf;
        }



        public string SourceDir { get; }
        public string DestinationDir { get; }
        public bool DeleteSource { get; }
        public bool WhatIf { get; }
    }
}
