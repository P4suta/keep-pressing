# KeepPressing

[![CI](https://github.com/P4suta/keep-pressing/actions/workflows/ci.yml/badge.svg)](https://github.com/P4suta/keep-pressing/actions/workflows/ci.yml)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

An auto-clicker for Windows. Repeat or hold mouse clicks and key presses, started and stopped with a global hotkey.

## Features

- **Mouse repeat** — left/right/middle button, at the current cursor position or a fixed point (capture it from the screen with F8)
- **Key repeat** — any key
- **Hold mode** — press down on start, release on stop
- **Global hotkey** (default F6, selectable from F5–F10) — start/stop while the target app keeps focus
- **Portable** — no install, no registry or MSIX entries; copy the folder to use, delete it to remove

## Usage

1. Extract the zip anywhere and run `KeepPressing.exe` in the folder root (the payload is in `app/`; keep the folder together and don't move `app/`).
2. Pick the target (mouse/keyboard), the action (repeat/hold), and the interval.
3. Focus the app you want to drive and press **F6** to start/stop.

To uninstall, just delete the folder.

For a fixed position, choose "capture from screen", move the mouse to the target, and press **F8** to confirm (**Esc** to cancel). F8/Esc are reserved by the app only during capture.

## Build

The toolchain is managed by [mise](https://mise.jdx.dev/) (`mise.toml` pins the .NET SDK and [just](https://just.systems/)). Tasks live in the [justfile](justfile).

```
just setup      # one-time: install the pinned toolchain + git hooks (lefthook)
just build      # build everything (Debug)
just test       # run tests
just publish    # portable publish (x64) -> dist/KeepPressing
just clean      # remove bin/obj
```

The root launcher is built with NativeAOT, which needs **VS C++ Build Tools (MSVC)**. Without it, `just publish` skips the launcher; start `app/KeepPressing.exe` directly instead.

Commits follow [Conventional Commits](https://www.conventionalcommits.org/) (enforced locally by the `just setup` git hook and on PRs by the title check) — this feeds automated versioning.

## Releasing

Versioning and releases are automated with [release-please](https://github.com/googleapis/release-please): merging its Release PR bumps the version + `CHANGELOG.md` and publishes a signed GitHub Release. See [docs/RELEASING.md](docs/RELEASING.md) for the flow and one-time activation.

## Known limitations

1. **Cannot send to elevated windows** (UIPI). Unless this app runs elevated, input to elevated apps is silently dropped.
2. **No Up guarantee on force-kill** — if killed via `taskkill /F` etc. during a hold, a key/button may stay down. Press the physical key/button once to clear it.
3. **Key hold does not emulate OS auto-repeat** — Down is sent once (works for games that read key state via `GetAsyncKeyState`).
4. **Repeat rate is approximate** — the actual `PeriodicTimer` rate depends on the OS timer resolution; ~10ms (≈100/s) is a practical floor.
5. **Some apps ignore synthetic input** — games reading Raw Input directly or anti-cheat-protected apps may ignore or detect it.
6. **F8/Esc are reserved during position capture** (capture only; shown in the UI).
7. **Starting in "current cursor position" mode via the button clicks on this app** — prefer starting with the hotkey (hinted in the UI).

## License

[Apache License 2.0](LICENSE).
