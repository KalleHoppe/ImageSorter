using ImageSorter;
using ImageSorter.Domain;

LogUtility.Init();

Input inputArgs;
List<string> _movedFiles = new List<string>();

if (args.Length < 1)
    {
        Console.WriteLine("Source and Destination directory missing");
        Console.WriteLine("Usage: ImageSorter.exe <source dir> <destination dir> [params](optional)");
        Console.WriteLine("Params:");
        Console.WriteLine("-whatif \t Runs the script and displays output without commiting the canges.");
        Console.WriteLine("-d \t Deletes the source files sorting and copying the files to the destination folder.");
        Console.WriteLine("-heic2jpg \t Also creates a JPEG copy alongside any HEIC/HEIF file that is moved.");
        LogUtility.WriteToLog("No Params", LogUtility.Level.Error);
        return;
    }

inputArgs = Util.GetArgs(args);

try
{
    Validation.ValidateDirectory(inputArgs.SourceDir);
}
catch (DirectoryNotFoundException dirEx)
{
    Util.Print($"The selected directory could not be found: {inputArgs.SourceDir} - {dirEx.Message}");
    return;
}

try
{
    Validation.ValidateDirectory(inputArgs.DestinationDir);
}
catch (DirectoryNotFoundException dirEx)
{
    Util.Print($"The selected directory could not be found: {inputArgs.DestinationDir} - {dirEx.Message}");
    return;
}

//Get files from source dir
Util.Print("----------- Image sorting started -----------");
Util.Print("Getting files...");
    var files = Util.GetAllFiles(inputArgs.SourceDir);
Util.Print("Done getting files");
    //might do an unneccecary list loop
    //LogUtility.WriteToLog("Files to move: " + files.Count(), LogUtility.Level.Info);

    //Loop thru files

    // Decoding+encoding a HEIC file is CPU- and memory-heavy (each decoded frame is an
    // uncompressed pixel buffer, e.g. ~48MB for a 12MP photo) - running unlimited HEIC
    // conversions concurrently across every worker thread at once can exhaust memory on
    // large batches and make the whole run appear to hang. Cap concurrency only when the
    // flag is on; every other file still gets the default full parallelism.
    var parallelOptions = new ParallelOptions
    {
        MaxDegreeOfParallelism = inputArgs.ConvertHeicToJpeg ? Math.Max(1, Environment.ProcessorCount / 2) : -1
    };

    // Per-file detail (moved/skipped/converted/etc.) goes to the log only - see Util.LogOnly.
    // The console instead gets just this running counter, so a large batch always shows visible
    // progress instead of going silent for the several seconds a HEIC decode can take.
    var totalFiles = files.Count;
    var processedCount = 0;

    Parallel.ForEach(files, parallelOptions, file =>
    {
        {
            //For each file read date from exif
            var fileDate = Util.ParsePhotoDate(file);
            if (fileDate.HasValue)
            {
                //Create new paths
                var newDesitnationFolder = Util.GetNewDestinationFolder(inputArgs, fileDate);
                var newFullPath = Path.Combine(newDesitnationFolder, Path.GetFileName(file));

                byte[]? heicJpegBytes = null;
                if (inputArgs.ConvertHeicToJpeg && HeicConverter.IsHeicFile(file))
                {
                    if (inputArgs.WhatIf)
                        Util.LogOnly("Would create JPEG copy: " + Path.ChangeExtension(newFullPath, ".jpg"));
                    else
                        heicJpegBytes = HeicConverter.TryConvertToJpeg(file);
                }

                Util.CopyFile(_movedFiles, newDesitnationFolder, newFullPath, file, inputArgs.WhatIf, heicJpegBytes);
            }

            var completed = Interlocked.Increment(ref processedCount);
            var percent = totalFiles == 0 ? 100 : completed * 100 / totalFiles;
            Console.WriteLine($"Processed {completed}/{totalFiles} files ({percent}%)");
        }
    });

    Util.Print(_movedFiles.Count() + " copied to new folders");
    if (inputArgs.DeleteSource)
        Util.DeleteCopiedFiles(_movedFiles, inputArgs.SourceDir, inputArgs.WhatIf);


    Util.Print("----------- Image sorting finished -----------");
    Util.Print(_movedFiles.Count() + " files have been sorted to the new " + inputArgs.DestinationDir);
    Util.Print("Log file: " + LogUtility.CurrentLogFileUri);
    Console.ReadLine();

Serilog.Log.CloseAndFlush();
