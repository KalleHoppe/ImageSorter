# ImageSorterCore

A .net Core version of ImageSorter

## Usage

```text
ImageSorter.exe <source dir> <destination dir> [params](optional)
```

### Params

| Flag | Description |
| --- | --- |
| `-whatif` | Runs the script and displays output without committing the changes. |
| `-d` | Deletes the source files after sorting and copying them to the destination folder. |
| `-heic2jpg` | Also creates a JPEG copy alongside any HEIC/HEIF file that is moved. See [docs/adr/0002-heic-to-jpeg-conversion.md](docs/adr/0002-heic-to-jpeg-conversion.md) for details and licensing considerations. |
