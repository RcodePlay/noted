# NoteD

A simple TUI markdown note taker made in C#.

## Features
- Auto-save
- Settings in settings.json
- Fuzzy search
- Note management inside folders
- Command Palette (VSCode-style)
- Color themes (+ support for custom themes)
- #Tag support inside notes for faster searches
- Smart status bar (character & word count, cursor position)
- Recent notes list
- External editor hook

## Setup
1. Build from source or download compiled binary from the latest release.

2. To use the external editor hook, make sure the external editor's binary is in the environment PATH.

3. When setting up a password, make sure to store it properly, if you forget it, it can't be reset, so you'll lose access to all your files. **I do not take responsibility for any important file loss!**

4. On startup of NoteD, you have to give it time to decrypt all of the files. To increase encryption and decryption speed, you need to change the `DegreeOfParallelism` variable inside of Security.cs to a higher number to use more CPU threads. Also when you leave NoteD, it starts encrypting all the files, you have to wait until it finishes, otherwise your files will NOT be protected.