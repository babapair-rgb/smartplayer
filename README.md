# LearnPlay AI — Phase 1 (Video Player)

A native Windows video player, built as the foundation for LearnPlay AI's later
AI-assisted learning features (transcription, notes, diagrams, chat — each a
separate phase).

## Prerequisites

- Windows 10/11
- .NET 8 SDK

## Build and run

    dotnet build
    dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj

## Publishing a standalone build

    dotnet publish src/LearnPlayAI.App -c Release -r win-x64 --self-contained

Ship the whole `publish` output folder (the `.exe` plus its native DLLs and
`libvlc/` subfolder) — the target machine doesn't need .NET installed.

**Do not add `-p:PublishSingleFile=true`.** WPF apps are not supported with
single-file publish: the bundled assembly reports an empty `Assembly.Location`,
which breaks pack-URI resolution for XAML/BAML resources (e.g. `App.xaml`'s
styles), surfacing as a `StaticResourceExtension` exception at startup.

## Run the automated tests

    dotnet test

## Project layout

- `src/LearnPlayAI.Core` — domain models, repository interfaces, services, and the
  LibVLCSharp-based player engine. No WPF or SQLite dependency; unit-tested in
  isolation.
- `src/LearnPlayAI.Data` — SQLite implementations of Core's repository interfaces.
- `src/LearnPlayAI.App` — the WPF application: views, view models, themes, and
  keyboard shortcut handling.
- `tests/LearnPlayAI.Core.Tests` — tests against fakes/stubs, no SQLite involved.
- `tests/LearnPlayAI.Data.Tests` — tests against real (temporary) SQLite databases.

Application data (the SQLite database) is stored at
`%LOCALAPPDATA%\LearnPlayAI\learnplay.db`.

## Feature checklist (Phase 1 scope)

- [x] Play/pause/stop/resume, seek, variable speed (0.25x–4x), frame-stepping
- [x] MP4/MKV/AVI/MOV/WMV/FLV/WebM and other libVLC-supported formats
- [x] External and embedded subtitles, multiple audio/subtitle tracks, subtitle sync
- [x] Brightness/contrast/saturation adjustment
- [x] Screenshot capture, full-screen mode
- [x] Playlists (create/rename/delete, add/remove videos, auto-advance, skip-on-error)
- [x] Drag-and-drop, Library, Recently Watched, resume-from-last-position
- [x] Configurable keyboard shortcuts
- [x] Dark/light themes
- [x] Non-blocking error toasts; missing-file relocate/remove flow
- [ ] AI notes, transcription, diagrams, chat — out of scope for Phase 1

## Manual smoke test (run before considering Phase 1 done)

1. Launch the app; confirm the shell loads with the left nav, empty center pane,
   and the "AI Assistant" placeholder on the right.
2. Drag an MP4 onto the window — it registers in the Library and starts playing.
3. Verify play/pause, stop, seeking via the scrubber, and the volume slider.
4. Verify speed control (0.25x–4x, clamped), frame-stepping while paused, and
   screenshot capture (check `Pictures\LearnPlayAI\`).
5. Verify full-screen toggle restores the previous window state when exited.
6. With a multi-track video, verify audio/subtitle track switching and subtitle
   sync offset.
7. Verify brightness/contrast/saturation sliders visibly affect the picture.
8. Add a few videos to the Library; create a playlist and add them to it via the
   per-row playlist picker; play the playlist and confirm auto-advance to the next
   item on natural end, and skip-to-next on a corrupted item.
9. Stop partway through a video, revisit it from Recent, and confirm it resumes
   at the correct position.
10. Rename/delete a registered video's file on disk; confirm the relocate/remove
    dialog appears when trying to play it, and both paths (relocate, remove) work.
11. Toggle the theme from Settings; confirm it persists across an app restart.
12. Rebind a keyboard shortcut from Settings and confirm the new key works (and
    the old one no longer does).
13. Confirm every action above works with a 4K source file without dropped-frame
    stutter on typical hardware (hardware-accelerated decode via libVLC).
