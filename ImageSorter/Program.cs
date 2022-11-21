using ImageSorter;
using ImageSorter.Domain;

Input inputArgs;
List<string> _movedFiles = new List<string>();

if (args.Length < 1)
    {
        Console.WriteLine("Source and Destination directory missing");
        Console.WriteLine("Usage: ImageSorter.exe <source dir> <destination dir> [params](optional)");
        Console.WriteLine("Params:");
        Console.WriteLine("-whatif \t Runs the script and displays output without commiting the canges.");
        Console.WriteLine("-d \t Deletes the source files sorting and copying the files to the destination folder.");
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
    Util.Print($"The selected directory could not be found: {inputArgs.SourceDir} asd");
    return;
}

try
{
    Validation.ValidateDirectory(inputArgs.DestinationDir);
}
catch (DirectoryNotFoundException dirEx)
{
    Util.Print($"The selected directory could not be found: {inputArgs.DestinationDir} qwe");
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

    Parallel.ForEach(files , file =>
    {
        {
            //For each file read date from exif
            var fileDate = Util.ParsePhotoDate(file);
            if (fileDate.HasValue)
            {
                //Create new paths
                var newDesitnationFolder = Util.GetNewDestinationFolder(inputArgs, fileDate);
                var duplicateDestinationFolder = Util.GetDuplicateDestinationFolder(inputArgs.DestinationDir, fileDate);
                var newFullPath = newDesitnationFolder + Path.GetFileName(file);
                //Print(file + " ==> " + newFullPath);

                if (inputArgs.WhatIf)
                {
                     Util.Print("Will move " + file + " ==> " + newFullPath);
                    _movedFiles.Add(file);
                }
                else
                { Util.CopyFile(_movedFiles, newDesitnationFolder, newFullPath, duplicateDestinationFolder, file); }
            }
        }
    });

    Util.Print(_movedFiles.Count() + " copied to new folders");
    if (inputArgs.DeleteSource)
        Util.DeleteCopiedFiles(_movedFiles, inputArgs.WhatIf);


    Util.Print("----------- Image sorting finished -----------");
    Util.Print(_movedFiles.Count() + " files have been sorted to the new " + inputArgs.DestinationDir);
    Util.Print("See the log for details");
    Console.ReadLine();
