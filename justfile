# KeepPressing tasks — `just <recipe>` (toolchain managed by mise)
set windows-shell := ["bash", "-uc"]

sln := "KeepPressing.slnx"

# List recipes
default:
    @just --list

# One-time setup: install the pinned toolchain (mise.toml) + git hooks (lefthook).
setup:
    mise install
    mise x -- lefthook install

# Build everything (Debug)
build:
    mise x -- dotnet build {{sln}}

# Run tests
test:
    mise x -- dotnet test {{sln}}

# List known-vulnerable NuGet dependencies (including transitive)
audit:
    mise x -- dotnet list {{sln}} package --vulnerable --include-transitive

# Remove build outputs (bin/obj)
clean:
    rm -rf KeepPressing/bin KeepPressing/obj \
           KeepPressing.Core/bin KeepPressing.Core/obj \
           KeepPressing.Core.Tests/bin KeepPressing.Core.Tests/obj \
           KeepPressing.App.Tests/bin KeepPressing.App.Tests/obj

# Portable publish (default x64): launch shim at the root, payload isolated under app/.
# MUI/pdb pruning runs automatically via the csproj post-publish target on app/.
publish arch="x64":
    #!/usr/bin/env bash
    set -euo pipefail
    suffix=""
    if [ "{{arch}}" != "x64" ]; then suffix="-{{arch}}"; fi
    dist="dist/KeepPressing$suffix"
    rm -rf "$dist"
    # 1) Self-contained publish of the app into app/ (TrimPortableDistribution prunes app/).
    mise x -- dotnet publish KeepPressing -c Release -r win-{{arch}} -o "$dist/app"
    # 2) Place the NativeAOT launcher at the root. If publish fails without MSVC (VS C++ Build Tools), warn
    #    and skip. NativeAOT's native link finds the VC tools via vswhere, which may not be on PATH under
    #    mise, so prepend the standard VS Installer path first (no-op if absent). Harmless in CI.
    vsinst="/c/Program Files (x86)/Microsoft Visual Studio/Installer"
    if ! command -v vswhere >/dev/null 2>&1 && [ -x "$vsinst/vswhere.exe" ]; then export PATH="$vsinst:$PATH"; fi
    tmp="$(mktemp -d)"
    if mise x -- dotnet publish KeepPressing.Launcher -c Release -r win-{{arch}} -o "$tmp"; then
        cp "$tmp/KeepPressing.exe" "$dist/KeepPressing.exe"
        echo "Placed launcher: $dist/KeepPressing.exe"
    else
        echo "::warning:: Launcher (NativeAOT) publish failed. Install VS C++ Build Tools (MSVC) to get the root launch exe. For now, start $dist/app/KeepPressing.exe." >&2
    fi
    rm -rf "$tmp"
    # 3) Generate README.txt at the root from packaging/README.dist.txt as UTF-8 BOM + CRLF
    #    (strip CR then re-add, so the result is independent of the checked-out line endings).
    { printf '\xEF\xBB\xBF'; sed -e 's/\r$//' -e 's/$/\r/' packaging/README.dist.txt; } > "$dist/README.txt"
    # 4) Sanity-check required files.
    test -f "$dist/app/KeepPressing.exe" || { echo "App exe not found: $dist/app/KeepPressing.exe" >&2; exit 1; }
    echo "Published: $dist (root launch exe + README.txt + app/)"

# Regenerate the app icon from assets-src/icon-source.png (requires ImageMagick).
# Composites the glyph onto a white rounded tile; supersampled rounding, per-size Lanczos.
icons:
    #!/usr/bin/env bash
    set -euo pipefail
    src="assets-src/icon-source.png"
    out="KeepPressing/Assets"
    # Windows magick.exe can't interpret POSIX /tmp paths, so use a repo-relative work dir.
    work="assets-src/.iconwork"
    rm -rf "$work"; mkdir -p "$work"
    trap 'rm -rf "$work"' EXIT
    # White rounded tile (supersample at 2048, downscale to 1024 for smooth edges).
    magick -size 2048x2048 xc:none -fill white -draw "roundrectangle 0,0,2047,2047,360,360" -filter Lanczos -resize 1024x1024 PNG32:"$work/bg.png"
    magick "$src" -trim +repage -filter Lanczos -resize 800x800 -background none -gravity center -extent 1024x1024 PNG32:"$work/glyph.png"
    magick "$work/bg.png" "$work/glyph.png" -compose over -composite -colorspace sRGB PNG32:assets-src/icon-master.png
    master="assets-src/icon-master.png"
    # Multi-resolution ICO (Lanczos-downscale each size, then combine).
    for s in 16 24 32 48 64 128 256; do magick "$master" -filter Lanczos -resize ${s}x${s} PNG32:"$work/i$s.png"; done
    magick "$work/i16.png" "$work/i24.png" "$work/i32.png" "$work/i48.png" "$work/i64.png" "$work/i128.png" "$work/i256.png" "$out/AppIcon.ico"
    # Square tiles (MSIX/manifest).
    magick "$master" -filter Lanczos -resize 88x88   PNG32:"$out/Square44x44Logo.scale-200.png"
    magick "$master" -filter Lanczos -resize 24x24   PNG32:"$out/Square44x44Logo.targetsize-24_altform-unplated.png"
    magick "$master" -filter Lanczos -resize 48x48   PNG32:"$out/Square44x44Logo.targetsize-48_altform-lightunplated.png"
    magick "$master" -filter Lanczos -resize 300x300 PNG32:"$out/Square150x150Logo.scale-200.png"
    magick "$master" -filter Lanczos -resize 50x50   PNG32:"$out/StoreLogo.png"
    magick "$master" -filter Lanczos -resize 48x48   PNG32:"$out/LockScreenLogo.scale-200.png"
    # Wide tiles (white rounded tile + centered glyph).
    for name in Wide310x150Logo.scale-200 SplashScreen.scale-200; do
        magick -size 1240x600 xc:none -fill white -draw "roundrectangle 0,0,1239,599,108,108" -filter Lanczos -resize 620x300 PNG32:"$work/wbg.png"
        magick "$src" -trim +repage -filter Lanczos -resize 230x230 -background none -gravity center -extent 620x300 PNG32:"$work/wgl.png"
        magick "$work/wbg.png" "$work/wgl.png" -compose over -composite -colorspace sRGB PNG32:"$out/$name.png"
    done
    echo "icons regenerated from $src"
