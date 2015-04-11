# ImageSorter
Command line application for sorting images and avi files to a new target directory.

Sorts images by the date taken. The date is primary taken from the files EXIF data. If no EXIF data is found the files modified date is used.

The new folder structure created on the target directory is by date e.g. c:\images\YYYY\mm\dd. At the moment this is hardcoded.

The program works by copying the files into the new folder structure. Use the -d parameter to delete the files after the copy operation is verified.

## Usage
ImageSorter.exe \<source dir\> \<target dir\> [params]

### Params
* -whatif When used, prints and logs the outcome of the sorting without moving the files.
* -d Use to delete the files after copying. You will be asked before the deletion is done.

Hope it helps.
