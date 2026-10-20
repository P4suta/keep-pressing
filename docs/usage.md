# Usage and development

## Mouse and keyboard input

Run the root `KeepPressing.exe` from an extracted release folder.
Choose the mouse or keyboard target, then choose Repeat or Hold.
Repeat sends presses at the selected interval.
Hold sends one Down event on start and one Up event on stop.

Use **F6** to start or stop, or choose another hotkey from F5 through F10 in the app.
Focus the target app before starting.
Starting from the button in current cursor mode targets KeepPressing itself.

For a fixed mouse position, choose capture from screen, point at the target, then press **F8** to confirm or **Esc** to cancel.
The app reserves F8 and Esc only during position capture.

## Limitations

- Input to elevated windows needs KeepPressing to run at a matching privilege level because of Windows User Interface Privilege Isolation (UIPI).
- Force-killing the process during Hold may leave a key or mouse button down.
  Press and release the physical key or button to clear it.
- Hold sends one Down event without emulating the operating system's key repeat.
- Timing depends on the Windows timer resolution.
  The UI accepts intervals from 10 to 10,000 ms.
- Apps using Raw Input or anti-cheat software may ignore or detect synthetic input.

Stop the app before deleting its extracted folder to uninstall it.

## Build on Windows

Install [Git for Windows](https://git-scm.com/download/win), [mise](https://mise.jdx.dev/), and Visual Studio C++ Build Tools with the Windows SDK.
Use Git Bash for the recipes, including the NativeAOT launcher build.
`mise.toml` pins the other tools and enables reading the .NET version from `global.json`.
Both mise and the shared .NET installation use that exact SDK version.

```sh
mise install
mise x -- just build
mise x -- just test
mise x -- just audit
mise x -- just publish
```

`just publish` writes an unsigned portable folder to `dist/KeepPressing`.
A failed app or launcher build stops the command.
Open `dist/KeepPressing/KeepPressing.exe` to check the complete layout.
NativeAOT also needs the Visual C++ toolchain.

`mise x -- just setup` also installs the local Git hooks.
Commits use [Conventional Commits](https://www.conventionalcommits.org/).
The [release guide](RELEASING.md) covers signed distribution.
