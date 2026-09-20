# MultiShot v0.5 QA checklist

## Core regression

- [ ] Capture by window snapping
- [ ] Capture by free-region drag
- [ ] Pixel magnifier does not affect captured pixels
- [ ] Capture 3+ screenshots in one group
- [ ] Undo last screenshot
- [ ] Finish and copy group
- [ ] Paste multiple screenshots into ChatGPT Web
- [ ] Paste multiple screenshots into Feishu/Lark
- [ ] Paste multiple screenshots into WeChat
- [ ] Paste multiple screenshots into QQ
- [ ] Paste multiple PNG files into a Windows folder
- [ ] Temporary PNG files are cleaned only after Clipboard content changes
- [ ] UI returns to 0 pending screenshots after cleanup

## Language

- [ ] System default selects expected language
- [ ] Switch to 简体中文 without restart
- [ ] Switch to English without restart
- [ ] Main controller text changes
- [ ] Tray menu text changes
- [ ] Capture overlay text changes
- [ ] Toast text changes
- [ ] Error / information dialogs use selected language

## Hotkeys

- [ ] Change Capture hotkey and use it immediately
- [ ] Change Undo hotkey and use it immediately
- [ ] Change Finish hotkey and use it immediately
- [ ] Restart app and confirm custom hotkeys persist
- [ ] Try assigning duplicate shortcuts; app rejects them
- [ ] Try assigning a shortcut already occupied by another program; app rolls back to previous shortcuts
- [ ] Reset Defaults restores Ctrl+Shift+X / Ctrl+Shift+Z / Ctrl+Shift+Enter

## GitHub build

- [ ] `build.bat` compiles on Windows
- [ ] GitHub Actions `Windows build` workflow passes
- [ ] Uploaded artifact contains `MultiShot.exe`
