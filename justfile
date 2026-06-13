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

# 一時生成物(bin/obj)を一掃
clean:
    rm -rf KeepPressing/bin KeepPressing/obj \
           KeepPressing.Core/bin KeepPressing.Core/obj \
           KeepPressing.Core.Tests/bin KeepPressing.Core.Tests/obj

# ポータブル発行(既定 x64)。MUI/pdb 剪定は csproj の publish 後ターゲットが自動実行
publish arch="x64":
    mise x -- dotnet publish KeepPressing -c Release -r win-{{arch}} \
        -o dist/KeepPressing{{ if arch != "x64" { "-" + arch } else { "" } }}
