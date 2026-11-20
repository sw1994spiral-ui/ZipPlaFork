# ZipPla ダークモード版 ビルド手順（Windows）

## 前提

- Visual Studio 2019 または 2022（Community 可）
  - ワークロード「.NET デスクトップ開発」
  - 個別コンポーネント「.NET Framework 4.5.2 ターゲット パック」（無い場合は Microsoft の
    「.NET Framework 4.5.2 Developer Pack」を別途インストール）
- ソース: himamon/ZipPlaFork を clone し、`IMPLEMENTATION.md` の手順でダークモードを適用済みのもの

## 手順

1. `source/ZipPla.sln` を Visual Studio で開く。
2. 構成を **AutoBuild** にする（README 推奨。Release 構成はサムネイル読み込み失敗時にエラーダイアログが出る）。
3. ビルド → ソリューションのビルド。
   - Pre/PostBuildEvent で cscript による JScript 前後処理が自動で走る（Windows 標準機能、追加インストール不要）。
4. 成果物は `source/ZipPla/bin/AutoBuild/` に出る。`ZipPla.exe` を含むフォルダ一式が実行用。
5. 実行して、カタログ画面・ビューア・設定ダイアログがダーク配色になっていることを確認する。

## ダークモードの有効化

- 設定ファイルで有効化する場合: exe と同じフォルダの `config.xml`（初回起動で生成される）の
  `<GeneralConfig>` 要素内に `<DarkMode>true</DarkMode>` を追加して再起動。
- `IMPLEMENTATION.md` 手順5まで適用済みなら、設定ダイアログのチェックボックスでも切り替えられる（反映は再起動後）。

## GitHub Actions でビルドする場合（PC に Visual Studio を入れたくない場合）

1. フォークした自分のリポジトリに `build.yml`（同梱ファイル）を `.github/workflows/build.yml` として置く。
2. push するか、Actions タブから手動実行（workflow_dispatch）。
3. 完了後、Artifacts の `ZipPla-dark` から exe 一式をダウンロードする。
4. MSB3644 エラーが出た場合は build.yml 内のコメント指示に従い、参照アセンブリ取得ステップを有効化する。

## 注意

- 本アプリは .NET Framework 4.5.2 製のため、Linux / macOS 上ではビルド・実行できない（要 Windows 環境）。
- ライセンスは AGPL（原作・フォーク共通）。再配布する場合は AGPL の条件に従うこと。

## 検証結果（2026-11-21 追記）

- ダークモード適用済みソースは、Linux 上で .NET SDK 8 の MSBuild ＋ .NET Framework 4.5.2
  参照アセンブリを使い、コンパイルが通ることを確認済み（全 2,488 ソースファイル、エラー 0）。
- ただし Linux 版 MSBuild では .resx のバイナリリソース処理に
  `GenerateResourceUsePreserializedResources` が必要な都合上、配布用ビルドは
  Windows（Visual Studio）または GitHub Actions で行うことを推奨する。
- Pre/PostBuildEvent（cscript による前後処理）は Windows 前提のため、
  Linux 検証では無効化している。Windows / Actions では設計どおり実行される。
