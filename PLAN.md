# bookdex — Technical Plan

## Scope

A cross-platform (Windows 10/11 + macOS) desktop **ebook library manager and reader**:

- Scan one or more library folders and index ebooks: EPUB, PDF, MOBI/AZW3, FB2, and
  comic archives (CBZ/CBR).
- Parse normalized metadata (title, authors, series + index, ISBN, publisher, language,
  published date, subjects/tags, cover image).
- Build a local **SQLite** catalog with an **FTS5** full-text index over titles,
  authors, and extracted book text.
- Present a fast, cover-driven **library UI** with search, filters, and sort.
- Provide a **built-in reader** for EPUB/PDF/CBZ with per-book resume + bookmarks.
- Support **organization**: shelves/collections, tags, ratings, reading status
  (unread/reading/finished) and reading progress.
- Offer an **optional, local-only AI** layer for semantic search, summaries, and
  auto-tagging.
- Ship a **headless CLI** exposing the same core engine.

**In scope:** local, offline, single-user desktop app. Read-primarily; light metadata
editing. Privacy-first.

## Architecture / tech approach

- **Runtime:** .NET 8.
- **UI:** **Avalonia UI** (MVVM) for a single cross-platform codebase on Windows +
  macOS. (WPF rejected as Windows-only.)
- **Core (UI-free) library — `Bookdex.Core`:**
  - `IBookFormatReader` per format → normalized `BookMetadata` + `CoverImage` +
    `ExtractedText` (chunked). Backends behind interfaces so libraries are swappable:
    - EPUB: `VersOne.Epub` (or custom OPF/zip parse) for OPF metadata + spine text.
    - PDF: `PdfPig` for text extraction + first-page/cover render; `PDFsharp` if needed.
    - MOBI/AZW3: PalmDOC/KF8 header parse for metadata; text extraction best-effort.
    - FB2: XML parse.
    - CBZ/CBR: archive entry listing + embedded `ComicInfo.xml` (if present) + first
      image as cover (`System.IO.Compression` for zip; SharpCompress for rar).
  - `FormatRouter` dispatches by extension/magic bytes.
  - `ILibraryStore` — SQLite (via `Microsoft.Data.Sqlite`) schema: `books`, `authors`,
    `book_authors`, `series`, `shelves`, `tags`, `book_tags`, `reading_state`, plus an
    FTS5 virtual table `book_fts(title, authors, content)`.
  - `IScanner` — parallel folder enumeration (`System.IO.Enumeration`) with
    content-hash dedupe (size → partial hash → full hash), incremental rescan via
    stored file signature (path + size + mtime), and `FileSystemWatcher`-style watch on
    supported platforms.
  - `ICoverCache` — extracted covers stored/thumbnailed on disk (ImageSharp/SkiaSharp).
  - `ISearchService` — FTS5 keyword search + filters (author/series/tag/status).
  - `IReadingProgressStore` — per-book location + bookmarks.
  - `IBookAiService` — optional; see below.
- **Reader:**
  - EPUB: render spine HTML in Avalonia WebView (or HTML→flow document) with paging.
  - PDF: page render via PdfPig/PDFium images.
  - CBZ/CBR: image pager.
  - Resume position + bookmarks persisted per book.
- **CLI — `Bookdex.Cli`:** `scan`, `search`, `info`, `summarize`, `export` over the same
  `Bookdex.Core`, no UI dependency.
- **Local-AI — `IBookAiService`:** talks to a local OpenAI-compatible endpoint
  (Ollama / llama.cpp `server`, default `http://localhost:11434`). Embeddings
  (`nomic-embed-text` / `bge-small` class) cached in SQLite for semantic search;
  chat models (Llama 3.2 / Qwen2.5 / Phi-3-mini / MiniCPM class) for summaries &
  auto-tag suggestions. Reachability probe + graceful fallback to keyword search.
  **Off by default; local-only; only book text/metadata sent to localhost.**
- **Settings & data:** `%APPDATA%\bookdex` on Windows,
  `~/Library/Application Support/bookdex` on macOS (SQLite db, cover cache, config JSON).
- **Testing:** xUnit on `Bookdex.Core` (format readers with tiny fixture files, scanner
  dedupe/incremental logic, FTS query building, reading-progress store).

## Milestones

1. **M1 — Core model + metadata readers:** `Bookdex.Core` skeleton, `BookMetadata`
   model, EPUB + PDF + CBZ readers behind `IBookFormatReader` + `FormatRouter`; unit
   tests with fixtures.
2. **M2 — Catalog + full-text index:** SQLite schema + FTS5, `ILibraryStore`,
   `IScanner` with dedupe + incremental rescan; `bookdex scan`/`search`/`info` CLI.
3. **M3 — Library UI:** Avalonia MVVM shell, cover grid (virtualized), search bar,
   filters/sort, book detail pane.
4. **M4 — Reader:** in-app EPUB/PDF/CBZ reader with paging, resume, bookmarks.
5. **M5 — Organization:** shelves/collections, tags, ratings, reading status/progress.
6. **M6 — Local-AI (optional):** `IBookAiService`, embeddings + semantic search,
   summaries, auto-tag suggestions; reachability probe + fallback; off by default.
7. **M7 — Packaging & CI:** Windows self-contained zip + MSIX, macOS .app/.dmg
   (universal), GitHub Actions matrix (windows-latest + macos-latest).

## Non-goals

- No cloud sync, accounts, DRM removal, or online book stores.
- Not a full Calibre replacement (no plugin ecosystem, no device/e-reader sync in v1).
- No format *conversion* pipeline in v1 (may extract text from MOBI but not re-encode).
- No collaborative/multi-user features; single local user.
- No mandatory AI — every core feature works with AI disabled.

## Packaging / distribution target

- **Windows:** self-contained `win-x64` portable zip + MSIX package; code-sign later.
- **macOS:** `.app` bundle (universal: `osx-arm64` + `osx-x64`) delivered as `.dmg`;
  notarization deferred to a later milestone.
- **CI:** GitHub Actions matrix on `windows-latest` and `macos-latest` — build, run
  `Bookdex.Core` tests, and produce platform artifacts on tagged releases.
