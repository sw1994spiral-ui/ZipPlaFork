using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ZipPla
{
    /// <summary>
    /// 追加機能のダイアログ群（Designer を使わずコードのみで構築する）。
    /// CatalogForm 側の呼び出しは CatalogForm.Extras.cs にある。
    /// </summary>
    internal static class ExtraFormat
    {
        public static string FileSize(long size)
        {
            if (size < 0) return "－";
            if (size < 1024) return size + " B";
            if (size < 1024L * 1024) return (size / 1024.0).ToString("0.0") + " KB";
            if (size < 1024L * 1024 * 1024) return (size / 1024.0 / 1024.0).ToString("0.00") + " MB";
            return (size / 1024.0 / 1024.0 / 1024.0).ToString("0.00") + " GB";
        }
    }

    /// <summary>
    /// 複数ジャンルの AND 絞り込みダイアログ。チェックしたジャンルすべてを含むものだけが残る。
    /// 「現在の条件に追加」が OFF のときは絞り込み欄を置き換え、ON のときは filterAdd で追加する。
    /// </summary>
    public class GenreMultiFilterForm : Form
    {
        private readonly CheckedListBox lstGenres;
        private readonly CheckBox chkAppend;
        private readonly List<string> genres = new List<string>();

        public string[] SelectedGenres { get; private set; }
        public bool Append { get; private set; }

        /// <param name="genresWithCounts">ジャンル名と現在のカタログでの件数（件数は表示用）</param>
        public GenreMultiFilterForm(IReadOnlyList<KeyValuePair<string, int>> genresWithCounts)
        {
            Text = "複数ジャンルで絞り込み";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(320, 400);
            MinimumSize = new Size(260, 240);

            var lbl = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(6, 6, 6, 0),
                Text = "チェックしたジャンルをすべて含むものだけを表示します。",
            };

            lstGenres = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
            };
            if (genresWithCounts != null)
            {
                foreach (var pair in genresWithCounts)
                {
                    genres.Add(pair.Key);
                    lstGenres.Items.Add(pair.Value > 0 ? $"{pair.Key}（{pair.Value}）" : pair.Key);
                }
            }

            chkAppend = new CheckBox
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Padding = new Padding(6, 0, 0, 0),
                Text = "現在の絞り込み条件に追加する",
            };

            var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
            var btnOK = new Button { Text = "絞り込み", Dock = DockStyle.Right, Width = 84, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "キャンセル", Dock = DockStyle.Right, Width = 84, DialogResult = DialogResult.Cancel };
            btnOK.Click += (sender, e) =>
            {
                var selected = new List<string>();
                foreach (int index in lstGenres.CheckedIndices)
                {
                    selected.Add(genres[index]);
                }
                SelectedGenres = selected.ToArray();
                Append = chkAppend.Checked;
            };
            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnOK);
            AcceptButton = btnOK;
            CancelButton = btnCancel;

            Controls.Add(lstGenres);
            Controls.Add(chkAppend);
            Controls.Add(pnlButtons);
            Controls.Add(lbl);

            DarkTheme.Apply(this);
        }
    }

    /// <summary>
    /// ジャンル名の一括変更ダイアログ。旧ジャンルを選び、新しい名前を入力する。
    /// 実際のリネーム（現在のカタログ全ファイル＋マスターリスト）は CatalogForm 側で行う。
    /// </summary>
    public class GenreBulkRenameForm : Form
    {
        private readonly ComboBox cmbOld;
        private readonly TextBox txtNew;
        private readonly List<string> genres = new List<string>();

        public string OldGenre { get; private set; }
        public string NewGenre { get; private set; }

        public GenreBulkRenameForm(IReadOnlyList<KeyValuePair<string, int>> genresWithCounts)
        {
            Text = "ジャンル名の一括変更";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(340, 170);

            var lblOld = new Label { Location = new Point(12, 14), AutoSize = true, Text = "変更するジャンル:" };
            cmbOld = new ComboBox
            {
                Location = new Point(12, 36),
                Width = 316,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            if (genresWithCounts != null)
            {
                foreach (var pair in genresWithCounts)
                {
                    genres.Add(pair.Key);
                    cmbOld.Items.Add(pair.Value > 0 ? $"{pair.Key}（{pair.Value}）" : pair.Key);
                }
            }
            if (cmbOld.Items.Count > 0) cmbOld.SelectedIndex = 0;

            var lblNew = new Label { Location = new Point(12, 70), AutoSize = true, Text = "新しいジャンル名:" };
            txtNew = new TextBox { Location = new Point(12, 92), Width = 316 };

            var btnOK = new Button { Text = "変更", Location = new Point(162, 130), Width = 84, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "キャンセル", Location = new Point(252, 130), Width = 84, DialogResult = DialogResult.Cancel };
            btnOK.Click += (sender, e) =>
            {
                if (cmbOld.SelectedIndex < 0) return;
                OldGenre = genres[cmbOld.SelectedIndex];
                NewGenre = (txtNew.Text ?? "").Trim();
            };
            AcceptButton = btnOK;
            CancelButton = btnCancel;

            Controls.Add(lblOld);
            Controls.Add(cmbOld);
            Controls.Add(lblNew);
            Controls.Add(txtNew);
            Controls.Add(btnOK);
            Controls.Add(btnCancel);

            DarkTheme.Apply(this);
        }
    }

    /// <summary>重複チェックの1項目。</summary>
    public class DuplicateItemInfo
    {
        public string Path;
        public string Name;
        public long Size;
        public DateTime LastWriteTime;
        public bool IsDirectory;
    }

    /// <summary>重複チェックの1グループ（名前とサイズが一致する項目の集まり）。</summary>
    public class DuplicateGroupInfo
    {
        public string KeyName;
        public List<DuplicateItemInfo> Items = new List<DuplicateItemInfo>();
    }

    /// <summary>
    /// 重複ファイルの一覧ダイアログ。各グループの先頭1件以外に初期チェックを入れ、
    /// チェックした項目をごみ箱へ移動できる。削除した場合は DeletedAny が true になり、
    /// 呼び出し側（CatalogForm）が表示を再読み込みする。
    /// </summary>
    public class DuplicateCheckForm : Form
    {
        private readonly DataGridView dgv;
        private readonly Label lblSummary;
        private readonly List<DuplicateGroupInfo> groups;

        public bool DeletedAny { get; private set; }

        public DuplicateCheckForm(List<DuplicateGroupInfo> groups)
        {
            this.groups = groups ?? new List<DuplicateGroupInfo>();

            Text = "重複ファイルのチェック";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 480);
            MinimumSize = new Size(480, 300);

            var totalFiles = this.groups.Sum(g => g.Items.Count);
            var removable = this.groups.Sum(g => Math.Max(0, g.Items.Count - 1));
            var removableSize = this.groups.Sum(g => g.Items.Skip(1).Sum(i => Math.Max(0, i.Size)));
            lblSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(6, 6, 6, 0),
                Text = $"名前とサイズが一致する重複が {this.groups.Count} 組・{totalFiles} 件あります。各組の先頭以外にチェックが入っています（最大 {removable} 件・{ExtraFormat.FileSize(removableSize)} を削減可能）。",
            };

            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
            };
            var checkColumn = new DataGridViewCheckBoxColumn { HeaderText = "削除", Width = 44, SortMode = DataGridViewColumnSortMode.NotSortable };
            var nameColumn = new DataGridViewTextBoxColumn { HeaderText = "ファイル名", Width = 260, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable };
            var sizeColumn = new DataGridViewTextBoxColumn { HeaderText = "サイズ", Width = 84, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable };
            var dateColumn = new DataGridViewTextBoxColumn { HeaderText = "更新日時", Width = 124, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable };
            var dirColumn = new DataGridViewTextBoxColumn { HeaderText = "場所", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable };
            dgv.Columns.Add(checkColumn);
            dgv.Columns.Add(nameColumn);
            dgv.Columns.Add(sizeColumn);
            dgv.Columns.Add(dateColumn);
            dgv.Columns.Add(dirColumn);
            dgv.CellContentClick += (sender, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == checkColumn.Index)
                {
                    dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            foreach (var group in this.groups)
            {
                for (var i = 0; i < group.Items.Count; i++)
                {
                    var item = group.Items[i];
                    var rowIndex = dgv.Rows.Add();
                    var row = dgv.Rows[rowIndex];
                    row.Tag = item;
                    row.Cells[checkColumn.Index].Value = i > 0;
                    row.Cells[nameColumn.Index].Value = item.Name;
                    row.Cells[sizeColumn.Index].Value = ExtraFormat.FileSize(item.Size);
                    row.Cells[dateColumn.Index].Value = item.LastWriteTime.ToString("yyyy/MM/dd HH:mm");
                    row.Cells[dirColumn.Index].Value = Path.GetDirectoryName(item.Path);
                }
            }

            var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
            var btnDelete = new Button { Text = "チェックした項目をごみ箱へ移動", Dock = DockStyle.Left, Width = 210 };
            btnDelete.Click += BtnDelete_Click;
            var btnClose = new Button { Text = "閉じる", Dock = DockStyle.Right, Width = 84, DialogResult = DialogResult.Cancel };
            pnlButtons.Controls.Add(btnDelete);
            pnlButtons.Controls.Add(btnClose);
            CancelButton = btnClose;

            Controls.Add(dgv);
            Controls.Add(pnlButtons);
            Controls.Add(lblSummary);

            DarkTheme.Apply(this);
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            var targets = new List<DuplicateItemInfo>();
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.Cells[0].Value is bool && (bool)row.Cells[0].Value)
                {
                    var item = row.Tag as DuplicateItemInfo;
                    if (item != null) targets.Add(item);
                }
            }
            if (targets.Count == 0)
            {
                MessageBox.Show(this, "チェックされた項目がありません。", null, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var totalSize = targets.Sum(i => Math.Max(0, i.Size));
            if (MessageBox.Show(this, $"{targets.Count} 件（{ExtraFormat.FileSize(totalSize)}）をごみ箱へ移動します。よろしいですか？",
                null, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            var deletedPaths = new HashSet<string>();
            var failed = 0;
            foreach (var item in targets)
            {
                try
                {
                    if (item.IsDirectory)
                    {
                        FileSystem.DeleteDirectory(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    }
                    deletedPaths.Add(item.Path);
                }
                catch
                {
                    failed++;
                }
            }

            if (deletedPaths.Count > 0)
            {
                DeletedAny = true;
                for (var i = dgv.Rows.Count - 1; i >= 0; i--)
                {
                    var item = dgv.Rows[i].Tag as DuplicateItemInfo;
                    if (item != null && deletedPaths.Contains(item.Path))
                    {
                        dgv.Rows.RemoveAt(i);
                    }
                }
            }
            if (failed > 0)
            {
                MessageBox.Show(this, $"{failed} 件を移動できませんでした。", null, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (deletedPaths.Count > 0 && failed == 0)
            {
                MessageBox.Show(this, $"{deletedPaths.Count} 件をごみ箱へ移動しました。", null, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
