# MultiShot

[简体中文](README.zh-CN.md)

**Capture several screenshots. Paste them all at once.**

MultiShot is a lightweight Windows screenshot utility built around one small workflow: start a capture session, collect several screenshots, then paste the whole group with a single `Ctrl+V`.

## Why MultiShot

Most screenshot tools treat every capture as a separate action. MultiShot treats several captures as one temporary group.

Typical workflow:

1. Capture a browser page.
2. Switch windows and capture an error dialog.
3. Capture a console or settings panel.
4. Finish the group.
5. Paste all screenshots into ChatGPT, Feishu/Lark, WeChat, QQ, a Windows folder, or another app that accepts multiple files.

## Features

- Batch screenshot sessions.
- Window snapping: hover a window and click to capture it.
- Free-region capture: drag anywhere to override window snapping.
- Pixel magnifier while selecting a free region.
- Undo the last capture.
- Paste the whole group at once using Windows `FileDropList`.
- A vertically combined bitmap is also placed on the Clipboard as a fallback for apps that only accept one image.
- Safe temporary-file cleanup after the Clipboard no longer references the captured PNG files.
- System tray workflow and single-instance protection.
- **English and Simplified Chinese UI.**
- **Customizable global hotkeys**, saved between launches.
- No Electron, Node.js, Python, cloud service, account, or telemetry.

## Default hotkeys

| Action | Default |
| --- | --- |
| Capture | `Ctrl + Shift + X` |
| Undo last | `Ctrl + Shift + Z` |
| Finish and copy | `Ctrl + Shift + Enter` |

Open **Settings** from the controller or tray menu to change the shortcuts. The new shortcuts are registered immediately. If Windows reports that a shortcut is already occupied, MultiShot keeps your previous shortcuts.

Settings are stored locally in:

```text
%APPDATA%\MultiShot\settings.ini
```

## Language

MultiShot supports:

- System default
- 简体中文
- English

With **System default**, Windows UI culture decides which language is used. Language can be changed in Settings without restarting the app.

## Run from source

1. Clone or download the repository.
2. Run `run.bat`.
3. On first launch, `build.bat` compiles `MultiShot.cs` into `MultiShot.exe` using the C# compiler included with .NET Framework 4.x.

No additional runtime is required on a normal Windows installation with .NET Framework 4.x enabled.

> MultiShot is currently unsigned. Windows SmartScreen may warn about locally built or downloaded binaries. You can inspect `MultiShot.cs` and build it yourself.

## Build

```bat
build.bat
```

The output is:

```text
MultiShot.exe
```

The repository also includes a GitHub Actions workflow that builds the Windows executable and uploads a ZIP artifact on pushes, pull requests, and manual runs.

## Current compatibility validated in real use

The core multi-paste workflow has been tested successfully with:

- ChatGPT Web
- Feishu / Lark
- WeChat
- QQ
- Windows File Explorer folders

Other applications may interpret Clipboard formats differently.

## Design principle

MultiShot intentionally avoids becoming a full screenshot suite. OCR, cloud sync, screenshot history, AI analysis, and a large annotation editor are outside the current scope unless they directly improve the core workflow.

> **Capture continuously. Paste once.**

## Version

Current prototype: **v0.5.0**

See [CHANGELOG.md](CHANGELOG.md).

## License

A public open-source license has not been selected yet. Choose one before treating the repository as reusable open-source software. MIT is a common permissive option if that matches your intent.
