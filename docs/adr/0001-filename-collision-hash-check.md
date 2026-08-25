# 0001. Verify filename collisions with a content hash instead of assuming duplicates

## Status

Accepted (2026-08-25) — design agreed, not yet implemented.

## Context

Images and videos are merged into a common destination tree from many different
source locations. Two files can land on the same destination path (same
`yyyy/MM/dd` folder, same filename) either because they really are the same
file copied from two sources, or because two *different* photos/videos
happen to share a filename and a capture date (e.g. camera-generated names
like `IMG_1234.jpg` reused across devices or after a camera reset).

Today (`Util.CopyFile`, `Util.GetDuplicateDestinationFolder`), any filename
collision at the destination is treated the same way: the incoming file is
copied into a parallel `Duplicates/yyyy/MM/dd/` folder, unconditionally and
without checking whether the two files are actually the same content. This
means:

- Genuinely identical files are kept twice (wasted space, and a `-d` source
  delete is only "safe" because a copy exists *somewhere*, not because it was
  verified identical).
- Genuinely different files that collide on name+date are silently merged
  into the `Duplicates` bucket with no indication they are different photos,
  and a second collision inside `Duplicates` itself is silently dropped
  (never copied, never added to the moved-files list).
- Camera model metadata was considered as a comparison signal but is not
  read or used anywhere today.

We want a check that can tell, when a collision happens, whether the two
files are actually the same content or different content that happens to
collide on name and date.

## Decision

- **Comparison basis: byte-identical content**, not perceptual/visual
  similarity. We are not trying to catch re-encoded/re-compressed copies of
  the same photo (e.g. from different export services) — only genuine
  content matches. Camera model metadata is **not** used as a comparison
  signal; it adds no information on top of a content hash. It is still read
  (cheaply, alongside the existing EXIF date extraction) and surfaced in the
  log line for a confirmed mismatch, purely as human-readable context.

- **Lazy, collision-triggered only.** No pre-hashing of the whole source
  tree and no persistent hash index across runs. The check only runs inside
  `CopyFile`, at the point a destination path collision (`File.Exists`) is
  already detected.

- **Two-stage comparison for cost control:**
  1. Compare `FileInfo.Length` first. A size mismatch proves the files
     differ without reading either file's contents.
  2. Only if sizes match, stream-hash both files with SHA-256
     (`System.Security.Cryptography`, no new dependency) and compare
     digests.
  - If hashing fails (locked file, `IOException`, permissions, etc.), log
    the failure and **fail safe**: treat the files as different rather than
    silently assuming they're the same.

- **Case A — hash match (confirmed identical):**
  - Skip copying the file. The `Duplicates` folder concept is removed
    entirely — `GetDuplicateDestinationFolder`, the
    `duplicateDestinationFolder` parameter threading through `CopyFile`/
    `Program.cs`, and `LogUtility.LogDuplicate` are all deleted as dead
    code once nothing routes to them.
  - The source file is still recorded as safe to delete under `-d`, since a
    verified byte-identical copy already exists at the destination. This is
    a *stronger* safety guarantee than the old "duplicate folder always
    gets a copy" approach, because it's grounded in verified content
    equality rather than filename matching alone.
  - If the destination is missing a companion JSON sidecar that the source
    has, the sidecar is still copied even though the image copy is skipped.

- **Case B — hash mismatch (confirmed different file, same name+date):**
  - Copied into the normal date folder (not a special folder — it isn't a
    duplicate) under a disambiguated filename: the original name with an
    8-hex-character suffix taken from the SHA-256 digest, e.g.
    `IMG_1234_a3f9c2d1.jpg`. This makes multiple distinct collisions on the
    same name self-disambiguating without needing an incrementing counter
    or a scan of existing files.
  - A companion JSON sidecar is renamed to match the same suffix, preserving
    whichever of the two sidecar naming conventions
    (`name.ext.json` / `name.json`) it originally used, so the pairing
    convention `GetCompanionJsonFile` relies on stays intact.

- **Concurrency:** the existing `CopyFileLock` (which today wraps the whole
  of `CopyFile`, including the actual file write, purely to protect the
  shared `movedFiles` list) is narrowed. The `File.Exists` check, size
  check, and hashing all happen *outside* the lock, since they're read-only
  and safe to run in parallel across threads — mirroring how
  `ParsePhotoDate` already runs unlocked in `Program.cs`. The lock is
  re-acquired only for the final decide-and-write step (re-checking
  `File.Exists` to guard the race window, then the actual copy/skip/rename
  and the `movedFiles` mutation). This avoids one large video's hash
  computation blocking every other in-flight copy.

- **`-whatif` parity:** `CopyFile` is refactored to accept a `whatIf` flag
  so the same collision/hash/rename decision logic drives both real and
  dry runs. Previously `-whatif` was a separate inline block in
  `Program.cs` that never checked for collisions at all, so a dry run gave
  no preview of duplicate/rename behavior. After this change, `-whatif`
  reports what would be skipped, renamed, or (on a hash failure) treated as
  a fallback collision, without touching disk.

## Consequences

- No more redundant on-disk copies of files that are already verified
  identical; `-d` deletion remains safe because it is now gated on
  confirmed content equality rather than filename collision alone.
- Genuinely different files that collide on name+date are preserved (never
  silently dropped) and remain independently addressable via their hash
  suffix.
- Every filename collision now costs at least a `FileInfo.Length` check,
  and — when sizes match — a full streamed read of both files to hash them.
  For large video collections this is the primary cost of the feature, but
  it only applies on the (expected to be rare) collision path, not on every
  file processed.
- `CopyFile`'s signature and the `Program.cs` call sites change (new
  `whatIf` parameter, removal of `duplicateDestinationFolder`), and the
  `Duplicates` folder / `LogUtility.LogDuplicate` are removed — any
  external tooling or docs referencing the `Duplicates` folder need
  updating.
- This does not address near-duplicate detection (re-encoded/resized copies
  of the same photo). If that need comes up later, it is a separate,
  larger feature (perceptual hashing) and not an extension of this
  decision.
