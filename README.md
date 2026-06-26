# KeepPressing

[![CI](https://github.com/P4suta/keep-pressing/actions/workflows/ci.yml/badge.svg)](https://github.com/P4suta/keep-pressing/actions/workflows/ci.yml)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

Windows 用の連打ツール。マウスクリック・キーボードの**連打**と**長押し**を、グローバルホットキーで開始/停止できます。

## 特徴

- **マウス連打** — 左/右/中ボタン。現在のカーソル位置、または固定座標（画面から F8 でキャプチャ可能）
- **キーボード連打** — 任意のキーを割り当て可能
- **長押しモード** — 開始で押下しっぱなし、停止で解放
- **グローバルホットキー**（既定 F6、F5–F10 から変更可）— 対象アプリにフォーカスがあるまま開始/停止できる
- **ポータブル** — インストール不要・レジストリ/MSIX 登録なし。フォルダごとコピーして使い、フォルダ削除で完全撤去

## 使い方

1. zip を任意の場所に展開し、フォルダー直下の `KeepPressing.exe` を起動（本体一式は `app\` にあります。`app\` は移動・分離せず、フォルダーごと扱ってください）
2. 対象（マウス/キーボード）・動作（連打/長押し）・間隔を設定
3. 操作したいアプリにフォーカスを移し、**F6** で開始/停止

アンインストールはフォルダーを削除するだけです（レジストリ/MSIX 登録なし）。

固定座標は「画面から取得」→ 目標へマウスを移動 → **F8** で確定（**Esc** で取消）。キャプチャ中のみ F8/Esc を本アプリが専有します。

## ビルド

ツールチェーンは [mise](https://mise.jdx.dev/) で管理（`mise.toml` が .NET SDK と [just](https://just.systems/) を固定）。タスクは [just](https://just.systems/) に集約しています。

```
just build      # 全体ビルド(Debug)
just test       # Core の単体テスト
just publish    # ポータブル発行(既定 x64) → dist/KeepPressing
just clean      # bin/obj を一掃
```

各レシピの実体は `mise x -- dotnet …`（`justfile` 参照）。`just publish` は配布物を業界標準の二層レイアウトで生成します:

```
dist/KeepPressing/
├── KeepPressing.exe   ← これを起動（薄い NativeAOT ランチャー）
├── README.txt         ← 英日併記の使い方
└── app/               ← 本体一式（self-contained payload）
    ├── KeepPressing.exe
    └── …（.NET/WinAppSDK ランタイム）
```

ルートを「起動 exe 1 つ + README」に絞り、散らかるランタイム DLL を `app/` に隔離します。本体は self-contained 発行後、`KeepPressing.csproj` の `TrimPortableDistribution` ターゲットが ja/en 以外のローカライズ MUI と pdb を自動剪定して軽量に保ちます。

ルートのランチャー（`KeepPressing.Launcher`）は NativeAOT でビルドするため、ローカルで `just publish` までランチャーを生成するには **VS C++ Build Tools (MSVC)** が必要です。未導入の場合はランチャーのみスキップされ、`app/KeepPressing.exe` から直接起動できます（CI の windows ランナーは MSVC 同梱のため、配布物は常にランチャー付きで生成されます）。

開発時はビルド出力の `KeepPressing.exe`（`app/` 内、または `KeepPressing/bin/...`）を直接起動します（unpackaged アプリなので exe 直接起動が正規の方法）。

## アーキテクチャ

```
KeepPressing(App, WinUI 3) ──▶ KeepPressing.Core ◀── KeepPressing.Core.Tests
        │                            │
        │  Interop/ に Win32 を封じ込め │  Win32 参照ゼロの純粋ドメイン
        │  (SendInput / RegisterHotKey │  (ADT・状態機械・PeriodicTimer
        │   / GetCursorPos / IME)      │   ・TimeProvider)
```

- 入力対象（`InputTarget`）・押下モード（`PressMode`）・エンジン状態（`EngineState`）は private コンストラクタで閉じた abstract record 階層（代数的データ型）。「長押しなのに間隔を持つ」ような不正状態は型で表現不能
- 副作用は Core が定義する唯一のポート `IInputSynthesizer` のみを通る（ヘキサゴナル・ライト）
- 長押しの Up 送出は `try/finally` + 「`StopAsync` 完了 = Up 送出済み」の API 契約 + 非同期ウィンドウクローズの多層防御
- Core は `ConfigureAwait(false)` を CA2007=error で機械的に強制
- グローバルホットキーは `RegisterHotKey(hWnd=NULL)` + 専用スレッドのメッセージキュー受信（ウィンドウ不要、`MOD_NOREPEAT` 付与）

## 既知の制限

1. **管理者昇格ウィンドウへは送出不可**（UIPI）。本アプリを昇格して起動しない限り、昇格アプリには入力が届きません（無音で無視されます）
2. **強制終了時の Up 保証なし** — `taskkill /F` 等で落とした場合、長押し中のキー/ボタンが押しっぱなし扱いになることがあります。物理キー/ボタンを一度押せば解消します
3. **キーボード長押しは OS のオートリピートをエミュレートしません** — Down は 1 回だけ送出されます（`GetAsyncKeyState` 系で押下状態を見るゲームには有効）
4. **連打レートは近似値** — `PeriodicTimer` の実レートは OS のタイマー分解能に依存します。間隔 10ms（≈100 回/秒）が実用上の目安です
5. **合成入力を無視するアプリがあります** — Raw Input を直接読むゲームやアンチチート保護されたアプリは、合成入力を無視・検出することがあります
6. **座標キャプチャ中は F8/Esc を専有します**（キャプチャ中のみ。UI にも表示されます）
7. **「現在のカーソル位置」モードで開始ボタンから開始すると自アプリの上を連打します** — ホットキーでの開始を推奨（UI にもヒントを表示）

## ライセンス

[Apache License 2.0](LICENSE) で公開しています。
