# EriMixer 0.3.0

EriMixer is a small Windows audio control centre. It is freeware, with no feature restrictions, subscriptions, advertisements or telemetry. Optional Ko-fi donations: [Support EriVyra](https://ko-fi.com/erivyra).

## Downloads

The Windows x64 release candidate provides:

- **Installer:** `EriMixer-0.3.0-x64.msi` — per-user installation, Start menu shortcut, and upgrade support from v0.2.0.
- **Portable:** `EriMixer-0.3.0-portable-x64.zip` — extract to a folder and run `EriMixer.exe`.
- **Checksums:** `SHA256SUMS.txt` — SHA-256 values for both downloads.

Downloads require Windows 11 x64 and the **.NET 8 Desktop Runtime (x64)**. The runtime is not bundled. The MSI does not require administrator elevation and installs in the current Windows user profile.

## Installation and unsigned app guidance

The MSI and application are not code-signed. Windows may show SmartScreen or publisher warnings when opening the installer. Only continue if you downloaded the file from the official EriMixer release source and its SHA-256 matches `SHA256SUMS.txt`. Choose **More info → Run anyway** only if you trust that source and have checked the hash. The warning reflects the missing publisher signature; it is not an endorsement or proof of safety.

To install, open the MSI and follow Windows Installer. To remove it, use **Settings → Apps → Installed apps → EriMixer → Uninstall**. Your per-user settings are kept in `%LOCALAPPDATA%\EriMixer`.

For portable use, extract the ZIP to a writable folder and run `EriMixer.exe`. Exit the tray copy before switching between portable and installed copies because EriMixer allows one instance per Windows session. Portable mode does not create an uninstall entry.

## What’s included

- Per-application volume and mute controls
- Input and output default-device selectors
- Configurable global shortcuts for opening the mixer and cycling devices
- Four selectable themes
- Standalone and compact popup modes, with resize and Fit apps controls
- Tray controls and optional Start with Windows

Default-device switching uses a Windows audio interface that requires manual verification on each supported Windows setup. A cycle shortcut has no visible effect when there is only one active device.

## Privacy and support

EriMixer includes no telemetry or advertising. Ko-fi donations are optional: [Support EriVyra](https://ko-fi.com/erivyra). There is no purchase requirement.

## Release status

This is a release candidate. Windows CI checks the MSI database, package contents, portable ZIP and checksums. Eri has manually verified MSI installation and upgrade, themes, hotkeys, output-device cycling and uninstall. Uninstall retains the user's settings JSON files under `%LOCALAPPDATA%\\EriMixer`. Input-device cycling remains untested because only one microphone was available. The portable ZIP launch and startup/tray checks have not been confirmed.
