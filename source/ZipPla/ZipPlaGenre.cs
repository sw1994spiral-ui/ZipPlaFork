using Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ZipPla
{
    /// <summary>
    /// ジャンル専用機能（試作）の設定。タグの ZipTagConfig と同じ仕組みで、
    /// config.xml にジャンル候補の一覧（マスターリスト）を保存する。
    /// ファイル自体へのジャンルの保存はファイル名マーカーの "g" キー（ZipPlaInfo.GenreArray）が担う。
    /// </summary>
    public class ZipGenreConfig : Configuration
    {
        public string[] Genres = new string[0];
    }

    /// <summary>
    /// ジャンル専用機能（試作）のヘルパー。
    /// </summary>
    public static class ZipGenre
    {
        /// <summary>
        /// ジャンル絞り込み用の検索語を作る。
        /// ファイル名マーカー内の "g=ジャンル1,ジャンル2" という並びに対して、
        /// ワイルドカード語 "g=*ジャンル*" は「g= の後ろにそのジャンル名が現れる」ことと等価になり、
        /// タイトル中に同じ単語を含むだけのファイルには一致しない。
        /// ジャンル名には ZipPlaInfo.CanBeTag の制約で * ? や区切り文字が含まれないため、この埋め込みは安全。
        /// </summary>
        public static string GetFilterTerm(string genre)
        {
            return SearchManager.TrimAndPutQuotationIfNeeded("g=*" + genre + "*", stopwords: null);
        }

        /// <summary>保存済みのジャンル候補（マスターリスト）を返す。</summary>
        public static string[] GetMasterGenres()
        {
            var config = new ZipGenreConfig();
            var genres = config.Genres;
            return genres != null ? genres : new string[0];
        }

        /// <summary>ジャンル候補（マスターリスト）を保存する。失敗時は例外オブジェクト、成功時は null。</summary>
        public static Exception SaveMasterGenres(IEnumerable<string> genres)
        {
            var config = new ZipGenreConfig();
            config.Genres = genres != null ? genres.ToArray() : new string[0];
            return config.Save();
        }
    }

    /// <summary>
    /// ジャンル専用機能（試作）の付け外しダイアログ。
    /// 既存のタグ付け（右クリック→タグのチェック切替）と同じ三値セマンティクス:
    ///   チェック      → 選択中の全ファイルに付ける
    ///   チェックなし  → 選択中の全ファイルから外す
    ///   中間（横線）  → ファイルごとに現状維持（一部だけ付いているジャンルの初期状態）
    /// 候補はマスターリスト＋選択中ファイルに付いているジャンルの和集合で、
    /// 下部のテキストボックスから自由入力で追加できる（検証はタグと同じ ZipPlaInfo.CanBeTag）。
    /// Designer を使わずコードのみで構築し、試作の差分をこのファイル1本に閉じ込める。
    /// </summary>
    public class GenreEditForm : Form
    {
        private readonly DataGridView dgvGenreList;
        private readonly DataGridViewCheckBoxColumn checkColumn;
        private readonly DataGridViewTextBoxColumn nameColumn;
        private readonly TextBox txtNewGenre;
        private readonly Button btnAdd;
        private readonly Button btnOK;
        private readonly Button btnCancel;

        /// <summary>OK 後のジャンルごとの状態（ジャンル名 → 三値）。</summary>
        public Dictionary<string, CheckState> ResultStates { get; private set; }

        /// <summary>OK 後の候補一覧（自由入力で追加された分を含む）。マスターリスト保存用。</summary>
        public string[] AllGenres { get; private set; }

        /// <param name="candidates">候補ジャンル（マスターリスト＋選択中ファイルのジャンルの和集合）</param>
        /// <param name="initialStates">ジャンル名 → 初期の三値状態</param>
        public GenreEditForm(IReadOnlyList<string> candidates, IReadOnlyDictionary<string, CheckState> initialStates)
        {
            Text = "ジャンル編集";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(360, 420);
            MinimumSize = new Size(280, 240);

            dgvGenreList = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
            };
            checkColumn = new DataGridViewCheckBoxColumn
            {
                HeaderText = "",
                Width = 36,
                ThreeState = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            nameColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "ジャンル名",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            dgvGenreList.Columns.Add(checkColumn);
            dgvGenreList.Columns.Add(nameColumn);
            // チェックボックスはクリック1回で切り替わるようにする（タグのメニューと同じ感覚）
            dgvGenreList.CellContentClick += (sender, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == checkColumn.Index)
                {
                    dgvGenreList.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            if (candidates != null)
            {
                foreach (var genre in candidates)
                {
                    CheckState state;
                    if (initialStates == null || !initialStates.TryGetValue(genre, out state))
                    {
                        state = CheckState.Unchecked;
                    }
                    var rowIndex = dgvGenreList.Rows.Add();
                    var row = dgvGenreList.Rows[rowIndex];
                    row.Cells[checkColumn.Index].Value = state;
                    row.Cells[nameColumn.Index].Value = genre;
                }
            }

            var pnlAdd = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(6, 5, 6, 3) };
            txtNewGenre = new TextBox { Dock = DockStyle.Fill };
            txtNewGenre.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AddNewGenre();
                }
            };
            btnAdd = new Button { Text = "追加", Dock = DockStyle.Right, Width = 64 };
            btnAdd.Click += (sender, e) => AddNewGenre();
            pnlAdd.Controls.Add(txtNewGenre);
            pnlAdd.Controls.Add(btnAdd);

            var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
            btnOK = new Button { Text = "OK", Dock = DockStyle.Right, Width = 84, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "キャンセル", Dock = DockStyle.Right, Width = 84, DialogResult = DialogResult.Cancel };
            btnOK.Click += BtnOK_Click;
            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnOK);
            AcceptButton = btnOK;
            CancelButton = btnCancel;

            Controls.Add(dgvGenreList);
            Controls.Add(pnlAdd);
            Controls.Add(pnlButtons);

            DarkTheme.Apply(this);
        }

        private void AddNewGenre()
        {
            var genre = (txtNewGenre.Text ?? "").Trim();
            if (string.IsNullOrEmpty(genre)) return;
            if (!ZipPlaInfo.CanBeTag(genre))
            {
                MessageBox.Show(this, "ジャンル名には次の文字は使えません。\n\n\\ / : * ? \" < > | { } ; ,",
                    null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            foreach (DataGridViewRow row in dgvGenreList.Rows)
            {
                if (string.Equals(row.Cells[nameColumn.Index].Value as string, genre, StringComparison.OrdinalIgnoreCase))
                {
                    row.Cells[checkColumn.Index].Value = CheckState.Checked;
                    txtNewGenre.Text = "";
                    return;
                }
            }
            var rowIndex = dgvGenreList.Rows.Add();
            var newRow = dgvGenreList.Rows[rowIndex];
            newRow.Cells[checkColumn.Index].Value = CheckState.Checked;
            newRow.Cells[nameColumn.Index].Value = genre;
            txtNewGenre.Text = "";
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            var states = new Dictionary<string, CheckState>();
            var all = new List<string>();
            foreach (DataGridViewRow row in dgvGenreList.Rows)
            {
                var genre = row.Cells[nameColumn.Index].Value as string;
                if (string.IsNullOrEmpty(genre)) continue;
                var value = row.Cells[checkColumn.Index].Value;
                var state = value is CheckState ? (CheckState)value : CheckState.Unchecked;
                states[genre] = state;
                all.Add(genre);
            }
            ResultStates = states;
            AllGenres = all.ToArray();
        }
    }
}
