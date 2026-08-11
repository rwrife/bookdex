# bookdex

**Local, privacy-first ebook library manager & reader for Windows 10/11 and macOS.**

Index your ebook collection (EPUB / PDF / MOBI / AZW3 / FB2 / CBZ/CBR comics), search
by metadata and full text, read with a built-in reader, and organize with shelves,
tags, and reading progress — all offline. Optional local-AI adds book summaries,
"describe what you remember" semantic search, and auto-tagging using tiny local
models (Ollama / llama.cpp), never the cloud.

> Status: 🚧 Bootstrapping. See [PLAN.md](PLAN.md) and the [issue backlog](../../issues).

---

## Overview

bookdex scans one or more library folders, parses ebook metadata (title, authors,
series, ISBN, publisher, language, cover art), extracts readable text for full-text
search, and presents your collection in a fast, cover-driven library UI. A built-in
reader lets you actually read EPUB/PDF/CBZ without leaving the app, tracking your
position per book. Everything is stored in a local SQLite database — your library
never leaves your machine.

## Motivation

Ebook collections sprawl across folders, cloud drives, and download dumps. Calibre is
powerful but heavyweight and dated; most "readers" don't index or search across a
whole library; and cloud reading services harvest your reading data. bookdex aims to be
a lightweight, modern, **cross-platform** library manager + reader that is fast,
private, and pleasant — with optional local-AI that helps you *find the book you half
remember* without sending a single byte to a server.

## Use cases

- **Find a book you half-remember** — full-text search across titles, authors, and
  book contents ("that sci-fi novel about a generation ship and a librarian").
- **Tidy a messy collection** — dedupe by ISBN/content, normalize author names, fill
  in missing metadata and covers.
- **Read in-app** — open EPUB/PDF/CBZ in the built-in reader with bookmarks and resume.
- **Organize** — shelves/collections, tags, ratings, reading status (unread / reading /
  finished), and reading progress.
- **Semantic recall (optional local-AI)** — describe a plot or theme and get matching
  books via local embeddings; generate a spoiler-light summary of a book you're deciding
  whether to read.

## Supported formats (target)

| Format | Metadata | Full-text index | In-app reader |
|--------|:--------:|:---------------:|:-------------:|
| EPUB   | ✅ | ✅ | ✅ |
| PDF    | ✅ | ✅ | ✅ |
| MOBI / AZW3 | ✅ | ✅ | via convert/extract |
| FB2    | ✅ | ✅ | ✅ |
| CBZ / CBR (comics) | ✅ (embedded) | — | ✅ |

## How to use

### Windows 10/11 quickstart

1. Download the latest `bookdex-win-x64.zip` from Releases (or the MSIX installer).
2. Unzip and run `bookdex.exe`.
3. On first launch, click **Add Library Folder** and point it at your ebooks.
4. Let the initial scan complete — covers and metadata populate as it indexes.
5. Search from the top bar; double-click a book to open the reader.

### macOS quickstart

1. Download `bookdex-macos.dmg` (universal: Apple Silicon + Intel) from Releases.
2. Open the DMG and drag **bookdex.app** to Applications.
3. Launch it (first run: right-click → Open to clear Gatekeeper), then **Add Library
   Folder** and select your ebooks folder.
4. Wait for indexing, then search and read.

### Headless / power users

A `bookdex` CLI mirrors the core engine for scripting:

```
bookdex scan   ~/Books            # index a folder
bookdex search "generation ship"  # full-text search, prints matches
bookdex info   <book-id>          # dump normalized metadata
bookdex export --format csv       # export the catalog
```

## Example workflow

```
# 1. Index two library roots
bookdex scan "D:\Ebooks"
bookdex scan "D:\Comics"

# 2. Find that book
bookdex search "librarian generation ship" --limit 5

# 3. See its metadata and where it lives
bookdex info 4f2a

# 4. (optional) Ask local AI for a spoiler-light summary
bookdex summarize 4f2a        # requires a running local model; see below
```

## Local-AI integration (optional, off by default)

bookdex works fully without any AI. When you opt in, it talks to a **local**
OpenAI-compatible endpoint (Ollama or llama.cpp `server`) at `http://localhost:11434`
(configurable). No cloud, no API keys, no telemetry.

- **Semantic / "describe it" search** — text embeddings of book blurbs and content
  chunks (e.g. `nomic-embed-text`, `bge-small`) stored locally; query by description.
- **Summaries & blurbs** — small chat models (Llama 3.2, Qwen2.5, Phi-3-mini, MiniCPM
  class) generate spoiler-light summaries and back-cover blurbs from extracted text.
- **Auto-tagging** — suggest genres/themes/tags for review before applying.

Behavior:
- **Off by default.** Enable in Settings → Local AI.
- **Reachability probe** before use; if the model server is down, bookdex degrades
  gracefully to metadata + keyword full-text search.
- Only book text/metadata is sent to your local model — never over the network.

## Current status / milestones

- [ ] M1 — Core library model + format metadata readers (EPUB/PDF/CBZ)
- [ ] M2 — SQLite catalog + FTS5 full-text index + incremental rescan
- [ ] M3 — Cross-platform library UI (Avalonia) with cover grid + search
- [ ] M4 — Built-in reader (EPUB/PDF/CBZ) with resume + bookmarks
- [ ] M5 — Shelves, tags, ratings, reading status/progress
- [ ] M6 — Optional local-AI: semantic search, summaries, auto-tagging
- [ ] M7 — Packaging & CI (Windows zip/MSIX, macOS .app/.dmg, Actions matrix)

## License

MIT (see repository).
