# KeepPressing tasks — `just <recipe>`（ツールチェーンは mise 管理）
set windows-shell := ["bash", "-uc"]

sln := "KeepPressing.slnx"

# レシピ一覧
default:
    @just --list

# 全体ビルド(Debug)
build:
    mise x -- dotnet build {{sln}}

# Core 単体テスト
test:
    mise x -- dotnet test {{sln}}

# NuGet 依存(推移的含む)の既知脆弱性を一覧。CI は出力を解析して脆弱性検出で失敗させる
audit:
    mise x -- dotnet list {{sln}} package --vulnerable --include-transitive

# 一時生成物(bin/obj)を一掃
clean:
    rm -rf KeepPressing/bin KeepPressing/obj \
           KeepPressing.Core/bin KeepPressing.Core/obj \
           KeepPressing.Core.Tests/bin KeepPressing.Core.Tests/obj \
           KeepPressing.App.Tests/bin KeepPressing.App.Tests/obj

# ポータブル発行(既定 x64): ルート直下に起動ランチャー、本体一式は app/ に隔離(find-my-files 流の二層レイアウト)。
# 解凍したら「ルートの KeepPressing.exe を起動」と一目で分かる。MUI/pdb 剪定は csproj の publish 後ターゲットが app/ を対象に自動実行。
publish arch="x64":
    #!/usr/bin/env bash
    set -euo pipefail
    suffix=""
    if [ "{{arch}}" != "x64" ]; then suffix="-{{arch}}"; fi
    dist="dist/KeepPressing$suffix"
    rm -rf "$dist"
    # 1) 本体を app/ へ self-contained 発行(TrimPortableDistribution が app/ を剪定)
    mise x -- dotnet publish KeepPressing -c Release -r win-{{arch}} -o "$dist/app"
    # 2) ルートに NativeAOT ランチャーを配置。MSVC(VS C++ Build Tools)不在で発行に失敗したら警告のみでスキップ。
    #    NativeAOT の native リンクは vswhere 経由で VC ツールを探す。mise 環境では vswhere が PATH に無い
    #    ことがあるため、標準の VS Installer パスを補ってから発行する(存在しなければ何もしない)。CI では無害。
    vsinst="/c/Program Files (x86)/Microsoft Visual Studio/Installer"
    if ! command -v vswhere >/dev/null 2>&1 && [ -x "$vsinst/vswhere.exe" ]; then export PATH="$vsinst:$PATH"; fi
    tmp="$(mktemp -d)"
    if mise x -- dotnet publish KeepPressing.Launcher -c Release -r win-{{arch}} -o "$tmp"; then
        cp "$tmp/KeepPressing.exe" "$dist/KeepPressing.exe"
        echo "ランチャーを配置: $dist/KeepPressing.exe"
    else
        echo "::warning:: ランチャー(NativeAOT)の発行に失敗。VS C++ Build Tools(MSVC) を入れるとルート起動 exe が付きます。今は $dist/app/KeepPressing.exe で起動できます。" >&2
    fi
    rm -rf "$tmp"
    # 3) README.txt をルートに生成(テンプレ packaging/README.dist.txt を UTF-8 BOM + CRLF に変換)。
    #    チェックアウト時の改行(LF/CRLF どちらでも)に依存しないよう、一旦 CR を剥いてから付け直す。
    { printf '\xEF\xBB\xBF'; sed -e 's/\r$//' -e 's/$/\r/' packaging/README.dist.txt; } > "$dist/README.txt"
    # 4) 必須ファイルの自己検証
    test -f "$dist/app/KeepPressing.exe" || { echo "本体 exe が見つかりません: $dist/app/KeepPressing.exe" >&2; exit 1; }
    echo "発行完了: $dist (ルート起動 exe + README.txt + app/)"

# アプリアイコンを assets-src/icon-source.png から再生成(ImageMagick 必須)。
# ネイビーのグリフを白角丸タイルに合成。角丸はスーパーサンプル、全サイズ個別 Lanczos で高品質化。
icons:
    #!/usr/bin/env bash
    set -euo pipefail
    src="assets-src/icon-source.png"
    out="KeepPressing/Assets"
    # Windows の magick.exe は POSIX の /tmp パスを解釈できないため、リポジトリ相対の作業dirを使う
    work="assets-src/.iconwork"
    rm -rf "$work"; mkdir -p "$work"
    trap 'rm -rf "$work"' EXIT
    # 白角丸タイル(2048でスーパーサンプル → 1024へ縮小して縁を滑らかに)
    magick -size 2048x2048 xc:none -fill white -draw "roundrectangle 0,0,2047,2047,360,360" -filter Lanczos -resize 1024x1024 PNG32:"$work/bg.png"
    magick "$src" -trim +repage -filter Lanczos -resize 800x800 -background none -gravity center -extent 1024x1024 PNG32:"$work/glyph.png"
    magick "$work/bg.png" "$work/glyph.png" -compose over -composite -colorspace sRGB PNG32:assets-src/icon-master.png
    master="assets-src/icon-master.png"
    # 多解像度 ICO(各サイズを個別に Lanczos 縮小して結合)
    for s in 16 24 32 48 64 128 256; do magick "$master" -filter Lanczos -resize ${s}x${s} PNG32:"$work/i$s.png"; done
    magick "$work/i16.png" "$work/i24.png" "$work/i32.png" "$work/i48.png" "$work/i64.png" "$work/i128.png" "$work/i256.png" "$out/AppIcon.ico"
    # 正方タイル(MSIX/manifest 用)
    magick "$master" -filter Lanczos -resize 88x88   PNG32:"$out/Square44x44Logo.scale-200.png"
    magick "$master" -filter Lanczos -resize 24x24   PNG32:"$out/Square44x44Logo.targetsize-24_altform-unplated.png"
    magick "$master" -filter Lanczos -resize 48x48   PNG32:"$out/Square44x44Logo.targetsize-48_altform-lightunplated.png"
    magick "$master" -filter Lanczos -resize 300x300 PNG32:"$out/Square150x150Logo.scale-200.png"
    magick "$master" -filter Lanczos -resize 50x50   PNG32:"$out/StoreLogo.png"
    magick "$master" -filter Lanczos -resize 48x48   PNG32:"$out/LockScreenLogo.scale-200.png"
    # 横長タイル(白角丸 + 中央グリフ)
    for name in Wide310x150Logo.scale-200 SplashScreen.scale-200; do
        magick -size 1240x600 xc:none -fill white -draw "roundrectangle 0,0,1239,599,108,108" -filter Lanczos -resize 620x300 PNG32:"$work/wbg.png"
        magick "$src" -trim +repage -filter Lanczos -resize 230x230 -background none -gravity center -extent 620x300 PNG32:"$work/wgl.png"
        magick "$work/wbg.png" "$work/wgl.png" -compose over -composite -colorspace sRGB PNG32:"$out/$name.png"
    done
    echo "icons regenerated from $src"
