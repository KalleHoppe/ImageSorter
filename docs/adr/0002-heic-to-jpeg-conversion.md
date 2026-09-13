# 0002. Optional HEIC/HEIF to JPEG sibling conversion

## Status

Accepted (2026-09-13) — implemented.

## Context

HEIC/HEIF is Apple's default photo format on iOS, but many tools, viewers,
and older workflows still don't handle it well. Users sorting a mixed photo
library sometimes want a JPEG copy available alongside the original HEIC so
downstream tools that can't read HEIC still have something to work with.

Until now, ImageSorter never decoded or re-encoded image bytes — every write
was a plain `File.Copy`. This feature introduces the project's first
byte-level image transform, and with it, its first two new runtime
dependencies.

## Decision

- **New opt-in flag `-heic2jpg`.** When set, every HEIC/HEIF file that gets
  moved also gets a JPEG copy written alongside it at the destination. The
  original HEIC move (collision handling, companion JSON, delete-source) is
  completely unchanged; the JPEG is a pure addition.
- **Scope: primary image only.** Multi-frame HEIC containers (Live Photos,
  burst sequences) are not iterated — only the primary/default frame is
  converted.
- **Decoding: `Openize.HEIC` 26.9.0.** Pure managed .NET (netstandard2.0 /
  net6.0 / net8.0, no native dependency), which fits this project's
  Windows+Linux portability requirement. It only decodes to raw pixel
  arrays — it has no JPEG encoder.
- **Encoding: `SixLabors.ImageSharp`.** Added purely to encode the decoded
  pixel buffer to JPEG (`Image.LoadPixelData<Bgra32>` +
  `SaveAsJpeg(..., new JpegEncoder { Quality = 90 })`). Quality is hardcoded
  at 90 for v1, not configurable.
- **Conversion happens unlocked, before the existing `CopyFile` lock.**
  `Program.cs` decodes+encodes in memory inside the `Parallel.ForEach` body
  (same tier as `ParsePhotoDate`/`ResolveCollision`'s hashing), and only the
  resulting `byte[]?` is passed into `Util.CopyFile` as a new optional
  parameter. The actual `File.WriteAllBytes` for the JPEG happens inside
  `CopyFile`'s existing `CopyFileLock`, from a new helper
  (`WriteHeicJpegSibling`) called from all three collision branches
  (`CopyNewFile`, `SkipIdenticalFile`, `CopyDisambiguatedFile`) — the same
  branches that already call `CopyCompanionJsonFile` uniformly.
- **`-whatif` does not decode.** Under `-whatif`, `Program.cs` only logs the
  JPEG's intended path (`Path.ChangeExtension(newFullPath, ".jpg")`) without
  running the decoder — a deliberate trade of dry-run collision accuracy for
  keeping `-whatif` fast on large batches, since HEVC decoding is CPU-heavy.
- **JPEG filename and collision policy.** The JPEG takes the HEIC's own
  resolved destination filename with the extension swapped to `.jpg`
  (including any existing hash-disambiguation suffix). If something already
  exists at that path, the generated JPEG's own content is hash-compared
  against it: identical → skip; different → disambiguate with an 8-hex-char
  suffix from the JPEG's own SHA-256, mirroring
  `BuildDisambiguatedFileName`'s existing pattern for the primary file.
- **Backfill on rerun.** Because the JPEG-write helper runs from
  `SkipIdenticalFile` too, rerunning the tool with `-heic2jpg` newly enabled
  against an already-sorted tree will backfill missing JPEG siblings for
  HEICs that were sorted before the flag existed.
- **Decode/encode failures are always non-fatal.** A corrupt file or an
  unsupported HEIC variant is caught broadly, logged at `Warn`, and the
  function returns `null` — the HEIC still moves completely normally with no
  JPEG produced. There is no strict/fail-loud mode in v1.
- **Companion JSON is not duplicated.** The sidecar JSON, when present,
  stays paired only with the original HEIC.
- **The generated JPEG is never added to `movedFiles`.** That list is walked
  by `-d`/`DeleteSource` to remove *source*-side files; the generated JPEG
  has no source-side counterpart, so adding it there would make delete-source
  try to remove a path that never existed in the source tree.

## Licensing (accepted as disclosed risk, not resolved)

- **`Openize.HEIC`** ships under a proprietary "Openize License", not an
  OSI-approved license, and explicitly disclaims granting any patent license
  for HEVC/H.265 decompression technology. For a personal/OSS tool like
  ImageSorter (itself MIT-licensed, see `LICENSE`) this is treated as
  low-risk, consciously accepted. Anyone building or redistributing the
  compiled ImageSorter binary inherits this same exposure and should make
  their own determination — this decision does not resolve the HEVC patent
  question, it only discloses it.
- **`SixLabors.ImageSharp`** is under the Six Labors Split License: free for
  closed-source, for-profit use under $1M annual gross revenue, and a free
  "Community" license is available for eligible non-commercial/OSS use via
  `licensing.sixlabors.com`. No license key is configured for this project;
  building without one produces a cosmetic MSBuild warning
  ("No Six Labors license found...") but does not affect runtime behavior —
  verified directly: encoding succeeds identically with or without a
  configured key. A free Community key can be applied for later to silence
  the warning; it is not required for the feature to work.
- ImageSorter's own code remains MIT-licensed either way — these are new
  *runtime dependency* license terms that anyone building or distributing
  the compiled binary now also takes on, not a change to this repository's
  own license.

## Consequences

- ImageSorter gains its first two runtime dependencies for image decoding
  and encoding, and its first byte-level image transform (previously every
  write was `File.Copy`).
- `Openize.HEIC` 26.9.0 transitively depends on `MetadataExtractor 2.9.0`,
  lower than this project's direct `MetadataExtractor 2.9.3` reference.
  NuGet's version-unification picks the higher `2.9.3`
  (`NU1608` warning at build time) — if a future bump of either package
  inverts that relationship, this should be revisited.
- `Util.CopyFile`'s signature gained a new optional `byte[]? heicJpegBytes`
  parameter; existing call sites that don't care about this feature are
  unaffected (default `null`).
- `Domain.Input`'s constructor gained a new required `convertHeicToJpeg`
  parameter (no default, matching `delete`/`whatIf`'s existing style); the
  small number of call sites (tests and `Util.GetArgs`) were updated.
- Whether `MetadataExtractor 2.9.3`'s HEIF reader surfaces EXIF dates from
  HEIC files through the same directory types `MetadataDateReader` already
  checks (`ExifSubIfdDirectory`/`ExifIfd0Directory`) is unverified — this is
  a pre-existing gap independent of this feature (HEIC files already fell
  back to `LastWriteTime` if not), not something this decision resolves.
- Real end-to-end testing of `HeicConverter.TryConvertToJpeg`'s success path
  needs a genuine, structurally valid HEIC fixture, which — unlike this
  project's other tiny hand-crafted fixtures — can't be fabricated
  byte-by-byte. A properly-licensed sample (not copied from Openize's own
  non-commercial-only bundled samples) needs to be sourced separately and
  added to `Tests/Fixtures/`.
