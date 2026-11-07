# Phase 1: Native Windows Video Player — Design

## Context

This is Phase 1 of **LearnPlay AI**, an AI-powered video player for Windows (VLC-class
playback + an AI learning/note-generation assistant). The full project spans 8 phases
(player → transcription → AI notes/chapters → OCR/screenshots → diagrams →
chatbot → notes editor/export → packaging). Attempting to design all of this as one
spec would be too unfocused to build from, so each phase gets its own spec/plan cycle.

**This spec covers Phase 1 only: a fully working native video player**, with the
full application shell/layout in place (so later phases slot features into existing
panels rather than requiring a UI rewrite) but no AI functionality yet.

## Goals

- A professional, VLC-comparable Windows desktop video player.
- The application shell/layout that all later phases (notes, AI assistant, diagrams)
  will build into.
- A stable per-video identity (`VideoId`) that later phases can attach transcripts,
  notes, and screenshots to, without needing to redesign the data model.

## Non-goals (deferred to later phases)

- Audio extraction, speech-to-text, transcription.
- AI note generation, chapter detection, summaries.
- OCR, smart screenshots, diagrams/flowcharts/mind maps.
- AI chat assistant.
- Notes editor, export (PDF/DOCX/Markdown), quizzes/flashcards.
- Windows installer/MSIX packaging (Phase 8).

## Architecture

- **.NET 8, C#, WPF**, MVVM pattern using CommunityToolkit.Mvvm. The app will grow
  substantially over the remaining 7 phases, so View/ViewModel/Service separation is
  established from the start rather than retrofitted later.
- **LibVLCSharp** (official .NET bindings to libVLC) as the video engine. libVLC
  natively covers the format/codec matrix (MP4, MKV, AVI, MOV, WMV, FLV, WebM, …),
  external/embedded subtitles, multi-audio/subtitle track selection, variable-speed
  playback (0.25x–4x), frame-stepping, screenshot capture, and video filters
  (brightness/contrast/saturation) — so most of section 2's feature list is a thin
  wrapper over libVLC's API rather than custom playback engineering.
- **SQLite** (Microsoft.Data.Sqlite) for local persistence: library entries,
  playlists, resume positions, settings, window/layout state. No network calls in
  Phase 1 — the app is fully offline-capable at this stage.
- Project layout:
  - `src/App` — WPF UI (views, XAML, app bootstrap).
  - `src/Core` — domain models, player abstraction, services (Library, Playlist,
    Settings, Theme). UI-independent so it is unit-testable.
  - `src/Data` — SQLite schema, repositories.
  - `tests/` — unit tests for Core/Data.

## Components

- **PlayerEngine** (`src/Core`) — wraps LibVLCSharp's `MediaPlayer`:
  Play/Pause/Stop/Seek, `SetRate(0.25–4x)`, `StepFrame`, `SetAudioTrack`,
  `SetSubtitleTrack`, `Screenshot`, `SetVideoAdjust(brightness, contrast, saturation)`.
  Exposes playback position/state as observable events for the ViewModel layer.
- **MainShellView** (`src/App`) — the application layout from the parent spec:
  - Left sidebar: Library, Playlists, Recent Videos, Notes (disabled placeholder),
    Settings.
  - Center: video playback area (hosts PlayerEngine's render surface).
  - Right sidebar: AI Assistant panel and live notes panel — both present in the
    layout but inert/disabled in Phase 1.
  - Bottom: transport bar — scrubber/timeline, playback controls, chapter markers
    track (empty until Phase 3 adds chapter detection).
  - Panels are collapsible/resizable per section 10's requirements; dark/light theme
    toggle applies across the whole shell.
- **LibraryService** (`src/Core`) — registers opened/dropped files, computes a
  stable `VideoId` (GUID keyed by a partial content hash — first + last N MB plus
  file size, to avoid hashing entire large files — plus the file path), tracks
  recently-watched entries and last playback position per `VideoId`. This identity
  model is the explicit hook that Phase 2+ will use to associate transcripts, notes,
  screenshots, and chapters with a specific video, independent of the file being
  renamed/moved later (path is tracked, but `VideoId` identity survives a path
  change if content hash matches).
- **PlaylistService** (`src/Core`) — CRUD for user-defined playlists; playlist items
  reference `VideoId`s.
- **SettingsService** (`src/Core`) — theme preference, keyboard shortcut bindings,
  default volume, playback preferences.
- **ThemeManager** (`src/App`) — runtime dark/light switching via WPF resource
  dictionaries.
- **KeyboardShortcutManager** (`src/Core`/`src/App`) — maps configurable key
  bindings to PlayerEngine/UI actions (play/pause, seek, volume, fullscreen, frame
  step, screenshot, speed up/down, etc.).

## Feature checklist (from parent spec section 2)

All covered by PlayerEngine + MainShellView unless noted:

- Play/pause/stop/resume — PlayerEngine.
- MP4/MKV/AVI/MOV/WMV/FLV/WebM and other common formats — libVLC.
- External and embedded subtitles — libVLC + SetSubtitleTrack.
- Playback speed 0.25x–4x — PlayerEngine.SetRate.
- Frame-by-frame navigation — PlayerEngine.StepFrame.
- Screenshot capture — PlayerEngine.Screenshot.
- Full-screen mode — MainShellView.
- Audio track selection — PlayerEngine.SetAudioTrack.
- Subtitle synchronization (offset adjustment) — PlayerEngine, exposed in UI.
- Brightness/contrast/saturation — PlayerEngine.SetVideoAdjust.
- Playlist management — PlaylistService + UI.
- Drag-and-drop video files — MainShellView → LibraryService.
- Resume playback from last position — LibraryService + SQLite.
- Keyboard shortcuts — KeyboardShortcutManager.
- Recently watched videos — LibraryService.
- Multiple audio/subtitle tracks — PlayerEngine.
- High-resolution video including 4K — libVLC (hardware-accelerated decode where
  available).

## Data flow

1. User opens a file (File dialog, drag-drop, or double-click from Library/Recent).
2. `LibraryService` computes `VideoId`, registers/updates the library entry
   (path, hash, duration, last-watched timestamp).
3. `PlayerEngine` loads the media via libVLC and begins playback.
4. Playback events (position, track changes, state changes) flow from
   `PlayerEngine` to the active ViewModel, which updates the UI (scrubber,
   track menus, etc.).
5. Position updates are persisted to SQLite on a throttle (e.g., every few seconds
   and on pause/close), keyed by `VideoId`, enabling resume-from-last-position.
6. Settings/theme changes go through `SettingsService` and are persisted
   immediately.

## Error handling

- Corrupt or unsupported file: libVLC error is caught and surfaced as a
  non-blocking toast/notification; if part of a playlist, playback advances to the
  next item rather than halting.
- Missing file on resume (moved/deleted since last session): the Library/Recent
  entry is flagged "missing" with an option to relocate or remove it.
- SQLite corruption/migration failure: the existing db file is backed up
  (renamed) and a fresh schema is created, rather than crashing the app; the event
  is logged.

## Testing

- xUnit unit tests for `LibraryService`, `PlaylistService`, and `SettingsService`
  against a temporary/in-memory SQLite database — these are UI-independent by
  construction.
- Manual smoke-test checklist for actual playback and rendering behavior (format
  coverage, subtitle sync, speed control, frame stepping, screenshot, fullscreen),
  since WPF video rendering isn't practically covered by automated UI tests in
  Phase 1.
- No UI automation framework in Phase 1 (explicitly out of scope; can be
  reconsidered in a later phase if regressions become an issue).

## Open items carried to later phases

- Notes/AI Assistant panels exist in the shell but are inert — Phase 2+ will wire
  real content into them using the `VideoId` established here.
- Chapter markers track in the transport bar is present but empty until Phase 3
  (automatic chapter detection).
- Installer/packaging is explicitly deferred to Phase 8.
