# Image sorter

## Purpose

This is a console application used to do bulk sorting of images and videos from a source folder containing images to a destination folder.

The sorting is done primarly based on the date the media was created, image taken, video created. The date should be extracted from the medias metadata.

## Architecture

- The application is a .net application based on the latest .net version to date.
- The application should be portable and be able to run on both Windows and Linux.
- Use existing libraries to extract metadata. The one preffered at the moment is drewnoakes metadata-extractor-dotnet, but we use a fork of that from https://github.com/KalleHoppe/metadata-extractor-dotnet.

## Skills

Skills are organized into bucket folders under `skills/`:

- `engineering/` — daily code work
- `productivity/` — daily non-code workflow tools
- `misc/` — kept around but rarely used
- `personal/` — tied to my own setup, not promoted
- `in-progress/` — drafts not yet ready to ship
- `deprecated/` — no longer used

Every skill in `engineering/`, `productivity/`, or `misc/` must have a reference in the top-level `README.md` and an entry in `.claude-plugin/plugin.json`. Skills in `personal/`, `in-progress/`, and `deprecated/` must not appear in either.

Each skill entry in the top-level `README.md` must link the skill name to its `SKILL.md`.

Each bucket folder has a `README.md` that lists every skill in the bucket with a one-line description, with the skill name linked to its `SKILL.md`. Bucket `README.md`s and the top-level `README.md` group entries into **User-invoked** and **Model-invoked**.

Every `SKILL.md` is either user-invoked (`disable-model-invocation: true`, reachable only by the human) or model-invoked (model- or user-reachable). For the full definitions, description conventions, and why a user-invoked skill can invoke model-invoked skills but never another user-invoked one, see [docs/invocation.md](./docs/invocation.md).
