# ZipPla ダークモード実装手順

対象: himamon/ZipPlaFork（ZipPla雑改造版）、C# / Windows Forms / .NET Framework 4.5.2、ルート名前空間 `ZipPla`。
新規ファイル `DarkTheme.cs`（テーマ本体）を追加し、既存ファイルへの変更は最小限に留める設計。

## 調査で分かった前提

- 設定は独自基底クラス `Configuration`（exe 隣の `config.xml` に XmlSerializer で保存）。具象クラスは
  `GeneralConfig`（SettingForm.cs:1838）、`CatalogFormConfig`（CatalogForm.cs:29681）、`ViewerFormConfig`（ViewerForm.cs:15765）など。
  `Properties/Settings.settings` は空で未使用。
- 色の既存設定は部分的なもののみ（ビューア背景色 `BackColorOfBuiltInViewer` / `ViewerFormConfig.BackColor`、プロファイル色など）。
  UI 全体の配色設定は存在しない。
- `dark.ico` / `light.ico` はロゴアイコンのバリエーションで、UI テーマとは無関係。
- サムネイル表示 `ThumbViewer`（CatalogForm.cs:30370、ScrollableControl 派生）は `BackColor = SystemColors.Window` で初期化され、
  描画は BackColor から作ったブラシと `ZipTag.GetForeColor(BackColor)` による自動文字色で行う。
  → BackColor をダーク色に差し替えれば描画全体が追随する。
- CatalogForm には `SystemColorsWindowIsNearToWhite`（CatalogForm.cs:5743）など、OS のウィンドウ色に応じた色分岐が既にある。
  これらは SystemColors を見るため、アプリ側ダークモードとは連動しない（既知の残件として許容）。
- ToolStrip のカスタムレンダラーは未使用（CatalogForm.cs:25205 の言及はコメントアウト済み）。

## 適用手順

### 1. GeneralConfig に設定フィールドを追加（SettingForm.cs）

`GeneralConfig`（1838行目〜）の `CheckNgen` の隣に1行追加するだけ。既定は `false` なので既存ユーザーの挙動は変わらない。

```csharp
public bool CheckNgen = false;
public bool DarkMode = false; // ← 追加
```

### 2. DarkTheme.cs を配置し、プロジェクトに登録

- `DarkTheme.cs` を `source/ZipPla/` に置く。
- `source/ZipPla/ZipPla.csproj` の `<Compile Include="Configuration.cs" />`（2698行目付近）の近くに1行追加:

```xml
<Compile Include="DarkTheme.cs" />
```

### 3. Program.cs：メニュー類を一括ダーク化

`Main()` 内、フォーム（startForm）を生成する前（例: `Directory.SetCurrentDirectory(Application.StartupPath);` の直後）に追加:

```csharp
if (DarkTheme.Enabled) ToolStripManager.Renderer = DarkTheme.ToolStripRenderer;
```

これでアプリ内の全 ToolStrip / MenuStrip / ContextMenuStrip（動的生成分を含む）の背景・文字色がダークになる。

### 4. 各フォームにテーマ適用（1行ずつ）

各フォームの `InitializeComponent();` の直後に追加する:

```csharp
DarkTheme.Apply(this);
```

対象フォーム（優先順）:
CatalogForm（※コンストラクタではなく内部の `Constructor()` メソッド内の InitializeComponent 直後）、
ViewerForm、SettingForm、TagEditForm、FilterEditForm、KeyboardShortcutSettingForm、
MouseGestureSettingForm、SmartFolderSettingForm、ThumbnailCacheSettingForm、
ArchivePropertiesForm、AboutForm、MessageForm、ViewerFormMakeShortcutDialog。

`DarkTheme.Apply` はコントロールツリーを再帰走査し、標準のライト系デフォルト色だけを差し替える。
ThumbViewer（tvCatalog）は BackColor が SystemColors.Window のため自動で対象になり、内部描画も追随する。

### 5.（任意）SettingForm にチェックボックスを追加

既存の `CheckNgen` と同じパターン（`CurrentCheckNgen` プロパティ ⇔ `cbNgen` ⇔ 保存時 `config.CheckNgen = CurrentCheckNgen;` /
読込時 `CurrentCheckNgen = checkNgen;`）で `DarkMode` 用チェックボックス `cbDarkMode` を追加する。
デザイナでの配置は Visual Studio 上で行うのが安全（Designer.cs の手編集は避ける）。
チェックボックスが無くても、config.xml の `<GeneralConfig>` 要素内に `<DarkMode>true</DarkMode>` を書けば有効化できる。

### 6. 反映タイミング

テーマはフォーム生成時に適用されるため、ON/OFF の切り替えはアプリ再起動後に反映される。

## 未対応として残る箇所（仕様上の限界）

- ネイティブスクロールバーの色（Windows の描画依存）。
- TabControl のタブ帯そのもの（OS 描画。本体・TabPage はダーク化される）。
- システムダイアログ（MessageBox、ファイル選択ダイアログ等）。
- ExplorerTreeView が内部で使うネイティブツリー部分（外枠・通常コントロール部分のみ適用）。
- CatalogForm 内の `SystemColorsWindowIsNearToWhite` に基づくアクセント色分岐（OS テーマ連動のまま）。
- OS タイトルバーは DarkTheme 内の DWM 呼び出しでダーク化を試みる（Windows 10 2004 以降 / Windows 11 のみ、失敗時は無視）。

## ライセンス

原作・フォークとも AGPL。本変更はライセンス表記に一切触れない。
