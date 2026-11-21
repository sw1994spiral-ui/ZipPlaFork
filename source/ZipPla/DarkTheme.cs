using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZipPla
{
    /// <summary>
    /// ダークモード用テーマ。
    /// GeneralConfig.DarkMode が true のときだけ有効になる。
    /// 使い方:
    ///   1. Program.Main でフォーム生成前に ToolStripManager.Renderer = DarkTheme.ToolStripRenderer; を設定する（メニュー類を一括でダーク化）。
    ///   2. 各フォームの InitializeComponent() 直後で DarkTheme.Apply(this); を呼ぶ。
    /// 方針:
    ///   - 標準のライト系デフォルト色（SystemColors.Control / Window / ButtonFace / 白）のコントロールだけを差し替え、
    ///     アプリ側で意図的に付けられたカスタム色（ブックマーク色など）は潰さない。
    ///   - 自前描画コントロール（ThumbViewer 等）は BackColor から描画色を決めるため、BackColor の差し替えで追随する。
    /// </summary>
    public static class DarkTheme
    {
        // ---- パレット ----
        public static readonly Color FormBackground = Color.FromArgb(32, 32, 32);       // #202020
        public static readonly Color ControlBackground = Color.FromArgb(45, 45, 48);    // #2D2D30
        public static readonly Color InputBackground = Color.FromArgb(37, 37, 38);      // #252526
        public static readonly Color MenuBackground = Color.FromArgb(27, 27, 28);       // #1B1B1C
        public static readonly Color Foreground = Color.FromArgb(241, 241, 241);        // #F1F1F1
        public static readonly Color DisabledForeground = Color.FromArgb(133, 133, 133);
        public static readonly Color BorderColor = Color.FromArgb(63, 63, 70);          // #3F3F46
        public static readonly Color Accent = Color.FromArgb(0, 122, 204);              // #007ACC

        private static bool? enabledCache;

        /// <summary>ダークモードが有効かどうか。初回アクセス時に GeneralConfig から読み込む。</summary>
        public static bool Enabled
        {
            get
            {
                if (enabledCache == null)
                {
                    try { enabledCache = new GeneralConfig().DarkMode; }
                    catch { enabledCache = false; }
                }
                return enabledCache.Value;
            }
            set { enabledCache = value; }
        }

        private static ToolStripProfessionalRenderer renderer;
        /// <summary>メニュー／ツールバー用の共有ダークレンダラー。</summary>
        public static ToolStripProfessionalRenderer ToolStripRenderer
        {
            get
            {
                if (renderer == null) renderer = new DarkToolStripRenderer();
                return renderer;
            }
        }

        /// <summary>
        /// カタログのプロファイルカラー（背景色）をダークモード用に調整する。
        /// 明るい色は色味を残したまま暗く落とし、既に暗い色はそのまま使う。
        /// ダークモード無効時は何もしない。
        /// </summary>
        public static Color AdjustProfileColor(Color color)
        {
            if (!Enabled) return color;
            var luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
            if (luminance <= 150) return color;
            return Color.FromArgb(color.A,
                Math.Max(24, color.R / 5),
                Math.Max(24, color.G / 5),
                Math.Max(24, color.B / 5));
        }

        /// <summary>フォームとその子コントロールすべてにダーク配色を適用する。無効時は何もしない。</summary>
        public static void Apply(Form form)
        {
            if (!Enabled || form == null) return;

            form.BackColor = FormBackground;
            form.ForeColor = Foreground;
            TryEnableDarkTitleBar(form);
            TryEnableDarkScrollBars(form);
            if (!form.IsHandleCreated)
            {
                form.HandleCreated += (s, e) => TryEnableDarkTitleBar(form);
            }

            ApplyToChildren(form);
        }

        /// <summary>単体の ToolStrip（動的に生成される ContextMenuStrip など）に適用する。</summary>
        public static void ApplyToolStrip(ToolStrip toolStrip)
        {
            if (!Enabled || toolStrip == null) return;
            toolStrip.Renderer = ToolStripRenderer;
            toolStrip.BackColor = MenuBackground;
            toolStrip.ForeColor = Foreground;
            ApplyToolStripItems(toolStrip.Items);
        }

        private static void ApplyToChildren(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                ApplyToControl(control);
                if (control.ContextMenuStrip != null) ApplyToolStrip(control.ContextMenuStrip);
                if (control.HasChildren) ApplyToChildren(control);
            }
        }

        private static void ApplyToControl(Control control)
        {
            // OS描画のスクロールバー等をダークテーマにする
            TryEnableDarkScrollBars(control);

            // ToolStrip 系はレンダラーで描く
            var toolStrip = control as ToolStrip;
            if (toolStrip != null)
            {
                ApplyToolStrip(toolStrip);
                return;
            }

            // 画像そのものは触らない
            if (control is PictureBox) return;

            // 入力系コントロール
            if (control is TextBoxBase || control is ListBox || control is ComboBox)
            {
                control.BackColor = InputBackground;
                control.ForeColor = Foreground;
                return;
            }

            var treeView = control as TreeView;
            if (treeView != null)
            {
                treeView.BackColor = InputBackground;
                treeView.ForeColor = Foreground;
                treeView.LineColor = BorderColor;
                return;
            }

            var listView = control as ListView;
            if (listView != null)
            {
                listView.BackColor = InputBackground;
                listView.ForeColor = Foreground;
                return;
            }

            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.BackgroundColor = FormBackground;
                grid.GridColor = BorderColor;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = ControlBackground;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Foreground;
                grid.RowHeadersDefaultCellStyle.BackColor = ControlBackground;
                grid.RowHeadersDefaultCellStyle.ForeColor = Foreground;
                if (IsLightDefault(grid.DefaultCellStyle.BackColor) || grid.DefaultCellStyle.BackColor == Color.Empty)
                    grid.DefaultCellStyle.BackColor = InputBackground;
                if (IsLightText(grid.DefaultCellStyle.ForeColor) || grid.DefaultCellStyle.ForeColor == Color.Empty)
                    grid.DefaultCellStyle.ForeColor = Foreground;
                // 選択色などアプリ側で意味を持つ色は変更しない
                return;
            }

            var button = control as Button;
            if (button != null)
            {
                button.UseVisualStyleBackColor = false;
                button.BackColor = ControlBackground;
                button.ForeColor = Foreground;
                return;
            }

            // それ以外（Panel / Label / CheckBox / GroupBox / TabPage / 自前描画コントロール等）
            if (IsLightDefault(control.BackColor)) control.BackColor = ControlBackground;
            if (IsLightText(control.ForeColor)) control.ForeColor = Foreground;
        }

        private static void ApplyToolStripItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (IsLightText(item.ForeColor)) item.ForeColor = Foreground;
                var dropDownItem = item as ToolStripDropDownItem;
                if (dropDownItem != null && dropDownItem.HasDropDownItems)
                {
                    dropDownItem.DropDown.BackColor = MenuBackground;
                    dropDownItem.DropDown.ForeColor = Foreground;
                    ApplyToolStripItems(dropDownItem.DropDownItems);
                }
            }
        }

        private static bool IsLightDefault(Color color)
        {
            return color == SystemColors.Control
                || color == SystemColors.ControlLight
                || color == SystemColors.ControlLightLight
                || color == SystemColors.Window
                || color == SystemColors.ButtonFace
                || color == SystemColors.Menu
                || color == SystemColors.MenuBar
                || color == Color.White;
        }

        private static bool IsLightText(Color color)
        {
            return color == SystemColors.ControlText
                || color == SystemColors.WindowText
                || color == SystemColors.MenuText
                || color == Color.Black;
        }

        // ---- OS描画パーツ（スクロールバー等）をダークテーマにする ----
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        /// <summary>コントロールとそのスクロールバーに DarkMode_Explorer テーマを適用する。</summary>
        public static void TryEnableDarkScrollBars(Control control)
        {
            if (!Enabled || control == null) return;
            if (control.IsHandleCreated)
            {
                try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); }
                catch { /* 非対応OSでは何もしない */ }
            }
            else
            {
                control.HandleCreated += (s, e) =>
                {
                    try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); }
                    catch { }
                };
            }
        }

        // ---- OSタイトルバーをダークにする（Windows 10 2004以降 / Windows 11） ----
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private static void TryEnableDarkTitleBar(Form form)
        {
            try
            {
                if (!form.IsHandleCreated) return;
                var on = 1;
                // 20: DWMWA_USE_IMMERSIVE_DARK_MODE（現行）, 19: 旧バージョン用
                if (DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int));
                }
            }
            catch
            {
                // 対応していないOSでは何もしない
            }
        }
    }

    /// <summary>ダーク配色のカラーテーブル。</summary>
    public class DarkColorTable : ProfessionalColorTable
    {
        public DarkColorTable()
        {
            UseSystemColors = false;
        }

        private static readonly Color Back = DarkTheme.MenuBackground;
        private static readonly Color BackAlt = DarkTheme.ControlBackground;
        private static readonly Color Hover = Color.FromArgb(62, 62, 66);
        private static readonly Color Selected = DarkTheme.Accent;
        private static readonly Color Border = DarkTheme.BorderColor;

        public override Color ToolStripDropDownBackground => Back;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Selected;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => BackAlt;
        public override Color MenuItemPressedGradientMiddle => BackAlt;
        public override Color MenuItemPressedGradientEnd => BackAlt;
        public override Color ImageMarginGradientBegin => BackAlt;
        public override Color ImageMarginGradientMiddle => BackAlt;
        public override Color ImageMarginGradientEnd => BackAlt;
        public override Color ImageMarginRevealedGradientBegin => BackAlt;
        public override Color ImageMarginRevealedGradientMiddle => BackAlt;
        public override Color ImageMarginRevealedGradientEnd => BackAlt;
        public override Color MenuStripGradientBegin => Back;
        public override Color MenuStripGradientEnd => Back;
        public override Color ToolStripGradientBegin => BackAlt;
        public override Color ToolStripGradientMiddle => BackAlt;
        public override Color ToolStripGradientEnd => BackAlt;
        public override Color ToolStripBorder => Border;
        public override Color StatusStripGradientBegin => Back;
        public override Color StatusStripGradientEnd => Back;
        public override Color ButtonSelectedGradientBegin => Hover;
        public override Color ButtonSelectedGradientMiddle => Hover;
        public override Color ButtonSelectedGradientEnd => Hover;
        public override Color ButtonSelectedHighlight => Hover;
        public override Color ButtonSelectedHighlightBorder => Selected;
        public override Color ButtonPressedGradientBegin => Selected;
        public override Color ButtonPressedGradientMiddle => Selected;
        public override Color ButtonPressedGradientEnd => Selected;
        public override Color ButtonPressedHighlight => Selected;
        public override Color ButtonPressedHighlightBorder => Selected;
        public override Color ButtonCheckedGradientBegin => BackAlt;
        public override Color ButtonCheckedGradientMiddle => BackAlt;
        public override Color ButtonCheckedGradientEnd => BackAlt;
        public override Color ButtonCheckedHighlight => BackAlt;
        public override Color ButtonCheckedHighlightBorder => Selected;
        public override Color CheckBackground => Selected;
        public override Color CheckSelectedBackground => Selected;
        public override Color CheckPressedBackground => Selected;
        public override Color GripDark => Border;
        public override Color GripLight => BackAlt;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => BackAlt;
        public override Color OverflowButtonGradientBegin => BackAlt;
        public override Color OverflowButtonGradientMiddle => BackAlt;
        public override Color OverflowButtonGradientEnd => BackAlt;
    }

    /// <summary>
    /// ダーク用レンダラー。カラーテーブルに加え、メニュー項目の文字色と矢印色を強制的に明色にする
    /// （動的に生成されるコンテキストメニューでも文字が読めるようにするため）。
    /// </summary>
    public class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = (e.Item.Selected || e.Item.Pressed) ? Color.White : DarkTheme.Foreground;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = DarkTheme.Foreground;
            base.OnRenderArrow(e);
        }
    }
}
