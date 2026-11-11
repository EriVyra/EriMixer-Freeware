# EriMixer v0.3.0 — release candidate

Freeware Windows x64 audio mixer. No MSIX, app-store distribution, purchase requirement, subscriptions, ads or telemetry. Optional Ko-fi donations: [Support EriVyra](https://ko-fi.com/erivyra).

## Included

- The approved polished menu and theme interface.
- Input and output device selectors and configurable device-cycling hotkeys.
- Per-application volume and mute controls, tray behavior, popup resizing and Fit apps.
- Versioned per-user MSI with a major-upgrade path from v0.2.0.
- Portable ZIP and SHA-256 checksums.

## Requirements

Windows 11 x64 and .NET 8 Desktop Runtime (x64). MSI and app are unsigned; review the official source and verify SHA-256 before opening. The MSI installs for the current user and does not require administrator elevation.

## Candidate validation

Windows CI builds and checks the app and MSI database/payload. Eri manually verified MSI installation and upgrade, themes, hotkeys, output-device cycling, and uninstall. Uninstall leaves the user's settings JSON files under `%LOCALAPPDATA%\\EriMixer`. Input-device cycling remains untested because only one microphone was available. Portable ZIP launch and startup/tray behavior have not been confirmed.

