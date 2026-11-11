# EriMixer v0.3.0 release-candidate QA

Use a Windows 11 x64 system with .NET 8 Desktop Runtime x64. Exit any existing tray copy before launching the candidate.

- [x] MSI installation and upgrade from v0.2.0; existing app closed when prompted.
- [x] Themes and hotkeys.
- [x] Output-device cycling.
- [x] Uninstall; user settings JSON files remain under %LOCALAPPDATA%\\EriMixer.
- [ ] Verify MSI and portable download hashes before opening.
- [ ] Extract and launch the portable ZIP; verify it works without installation.
- [ ] Check tray open/exit, startup toggle, and single-instance behavior.
- [ ] Check per-app volume sliders and mute controls with active audio.
- [ ] Check input selection/cycling with at least two active microphones and verify Windows' actual default after every switch. With only one microphone, this test is not demonstrable.
- [ ] Check all four themes in standalone and popup modes, menu contrast, hotkey persistence, popup resize, Fit apps, Escape, and display scaling if not already covered.

Windows CI cannot replace device and end-user checks. Input-device cycling remains unverified until at least two microphones are available.