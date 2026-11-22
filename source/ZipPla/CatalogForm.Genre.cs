using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ZipPla
{
    /// <summary>
    /// ジャンル専用機能（試作）の CatalogForm 側コード。
    /// 試作の差分を最小化するため、CatalogForm 本体への変更は
    ///   1. コンストラクタでの SetupGenreMenu() 呼び出し
    ///   2. 右クリックメニュー（タグ サブメニュー）への「ジャンル編集...」追加
    /// の2か所だけに留め、残りはすべてこのファイルに置く。
    /// ジャンルの保存は ZipPlaInfo.GenreArray（ファイル名マーカーの "g" キー）、
    /// 候補の保存は ZipGenreConfig（config.xml）が担う。
    /// </summary>
    public partial class CatalogForm
    {
        private ToolStripMenuItem genreToolStripMenuItem;
        private string[] genreScanCache;
        private string[] genreScanSource;
        private Dictionary<string, int> genreCountCache;
        private string[] genreCountSource;

        /// <summary>
        /// 現在のカタログ（ZipPathArray）にあるファイル名の g= からジャンルを集める。
        /// 同期スクリプト等でファイル名に付けられたジャンルは config のマスターリストに
        /// 入らないため、メニュー表示用にこちらでも拾う。結果は配列の参照が変わるまでキャッシュする。
        /// </summary>
        private string[] CollectCatalogGenres()
        {
            string[] zpa;
            lock (ZipPathArrayLocker)
            {
                zpa = ZipPathArray;
            }
            if (zpa == null) return new string[0];
            if (genreScanCache != null && ReferenceEquals(zpa, genreScanSource)) return genreScanCache;
            var set = new List<string>();
            var seen = new HashSet<string>();
            var regex = new System.Text.RegularExpressions.Regex(@"\{zpi\$[^}]*?g=([^;}]+)");
            foreach (var path in zpa)
            {
                if (string.IsNullOrEmpty(path)) continue;
                // カタログキャッシュ（最適化実験）: 検証済みキャッシュがあれば解析結果を再利用する。
                // キャッシュ側も同じ正規表現で解析しているため結果は同一になる。
                string[] cachedGenres;
                if (CatalogCache.TryGetGenres(path, out cachedGenres))
                {
                    if (cachedGenres != null)
                    {
                        foreach (var genre in cachedGenres)
                        {
                            if (seen.Add(genre)) set.Add(genre);
                        }
                    }
                    continue;
                }
                var name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name)) continue;
                var match = regex.Match(name);
                if (!match.Success) continue;
                foreach (var part in match.Groups[1].Value.Split(','))
                {
                    var genre = part.Trim();
                    if (genre.Length == 0) continue;
                    if (seen.Add(genre)) set.Add(genre);
                }
            }
            genreScanSource = zpa;
            genreScanCache = set.ToArray();
            return genreScanCache;
        }

        /// <summary>
        /// 現在のカタログにあるジャンルごとの件数（そのジャンルが付いたファイル数）を返す。
        /// CollectCatalogGenres と同じキャッシュ方式で、ZipPathArray の参照が変わるまで再利用する。
        /// </summary>
        private Dictionary<string, int> CollectCatalogGenreCounts()
        {
            string[] zpa;
            lock (ZipPathArrayLocker)
            {
                zpa = ZipPathArray;
            }
            if (zpa == null) return new Dictionary<string, int>();
            if (genreCountCache != null && ReferenceEquals(zpa, genreCountSource)) return genreCountCache;
            var counts = new Dictionary<string, int>();
            var regex = new System.Text.RegularExpressions.Regex(@"\{zpi\$[^}]*?g=([^;}]+)");
            foreach (var path in zpa)
            {
                if (string.IsNullOrEmpty(path)) continue;
                // カタログキャッシュ（最適化実験）: CollectCatalogGenres と同じく解析結果を再利用する
                string[] cachedGenres;
                if (CatalogCache.TryGetGenres(path, out cachedGenres))
                {
                    if (cachedGenres != null)
                    {
                        var perFileCached = new HashSet<string>();
                        foreach (var genre in cachedGenres)
                        {
                            if (!perFileCached.Add(genre)) continue;
                            int currentCached;
                            counts.TryGetValue(genre, out currentCached);
                            counts[genre] = currentCached + 1;
                        }
                    }
                    continue;
                }
                var name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name)) continue;
                var match = regex.Match(name);
                if (!match.Success) continue;
                var perFile = new HashSet<string>();
                foreach (var part in match.Groups[1].Value.Split(','))
                {
                    var genre = part.Trim();
                    if (genre.Length == 0 || !perFile.Add(genre)) continue;
                    int current;
                    counts.TryGetValue(genre, out current);
                    counts[genre] = current + 1;
                }
            }
            genreCountSource = zpa;
            genreCountCache = counts;
            return genreCountCache;
        }

        /// <summary>メインメニューに「ジャンル」絞り込みメニューを追加する。コンストラクタから1回だけ呼ぶ。</summary>
        private void SetupGenreMenu()
        {
            genreToolStripMenuItem = new ToolStripMenuItem("ジャンル");
            genreToolStripMenuItem.DropDownOpening += GenreToolStripMenuItem_DropDownOpening;
            menuStrip.Items.Add(genreToolStripMenuItem);
        }

        /// <summary>
        /// 「ジャンル」メニューを開くたびに、マスターリストから項目を作り直す。
        /// 各ジャンルのサブメニューは既存のタグ絞り込みボタンと同じ6操作で、
        /// 検索語の組み立てもタグと同じ filterAdd / cbFilter.Text 経由なのでタグ条件と併用できる。
        /// </summary>
        private void GenreToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var menu = genreToolStripMenuItem;
            menu.DropDownItems.Clear();
            var genres = new List<string>();
            foreach (var genre in ZipGenre.GetMasterGenres())
            {
                if (!genres.Contains(genre)) genres.Add(genre);
            }
            foreach (var genre in CollectCatalogGenres())
            {
                if (!genres.Contains(genre)) genres.Add(genre);
            }
            if (genres.Count == 0)
            {
                menu.DropDownItems.Add(new ToolStripMenuItem("（ジャンルはまだありません）") { Enabled = false });
                menu.DropDownItems.Add(new ToolStripMenuItem("ファイルの右クリック → タグ → ジャンル編集... から追加できます") { Enabled = false });
                return;
            }
            var genreCounts = CollectCatalogGenreCounts();
            foreach (var genre in genres)
            {
                var captured = genre;
                var term = ZipGenre.GetFilterTerm(captured);
                int genreCount;
                var label = captured.Replace("&", "&&");
                if (genreCounts.TryGetValue(captured, out genreCount) && genreCount > 0)
                {
                    label += $"（{genreCount}）";
                }
                var item = new ToolStripMenuItem(label);

                var sub = new ToolStripMenuItem[6];
                sub[0] = new ToolStripMenuItem($"「{captured}」を追加");
                sub[0].Click += (s, ev) => filterAdd(term, weak: true);
                sub[1] = new ToolStripMenuItem($"「{captured}」を含まない条件を追加");
                sub[1].Click += (s, ev) => filterAdd("-" + term, weak: true);
                sub[2] = new ToolStripMenuItem($"「{captured}」だけにする");
                sub[2].Click += (s, ev) => { cbFilter.Text = term; cbFilter.SelectAll(); };
                sub[3] = new ToolStripMenuItem($"「{captured}」以外にする");
                sub[3].Click += (s, ev) => { cbFilter.Text = "-" + term; cbFilter.SelectAll(); };
                sub[4] = new ToolStripMenuItem($"「{captured}」を OR 追加");
                sub[4].Click += (s, ev) => filterAdd("+" + term, weak: false);
                sub[5] = new ToolStripMenuItem($"「{captured}」を OR 除外追加");
                sub[5].Click += (s, ev) => filterAdd("+-" + term, weak: false);
                item.DropDownItems.AddRange(sub);

                menu.DropDownItems.Add(item);
            }
            menu.DropDownItems.Add(new ToolStripSeparator());
            var multiItem = new ToolStripMenuItem("複数ジャンルで絞り込み...");
            multiItem.Click += (s, ev) => ShowGenreMultiFilter();
            menu.DropDownItems.Add(multiItem);
            var renameItem = new ToolStripMenuItem("ジャンル名の一括変更...");
            renameItem.Click += (s, ev) => ShowGenreBulkRename();
            menu.DropDownItems.Add(renameItem);
        }

        /// <summary>
        /// 右クリックメニューの「ジャンル編集...」から呼ばれる。
        /// タグ適用フローと同じ安全策として、現在の ZipPathArray と一致する選択だけを対象にし、
        /// 実際のリネームは既存の RenameShownArchive に任せる。
        /// </summary>
        private void ShowGenreEditorForContextMenu(int[] selectedIndices, string[] selectedPaths, string[] selectedNames)
        {
            if (CreatingZipPathArray) return;
            if (selectedIndices == null || selectedPaths == null || selectedNames == null) return;
            var count = selectedPaths.Length;
            if (count == 0 || selectedIndices.Length != count || selectedNames.Length != count) return;

            var indices = new List<int>();
            var paths = new List<string>();
            var names = new List<string>();
            var infos = new List<ZipPlaInfo>();
            lock (ZipPathArrayLocker)
            {
                var zpa = ZipPathArray;
                if (zpa == null) return;
                for (var i = 0; i < count; i++)
                {
                    var index = selectedIndices[i];
                    var path = selectedPaths[i];
                    if (index < 0 || index >= zpa.Length || path != zpa[index]) continue;
                    indices.Add(index);
                    paths.Add(path);
                    names.Add(selectedNames[i]);
                    infos.Add(new ZipPlaInfo(path));
                }
            }
            if (paths.Count == 0) return;

            // 候補 = マスターリスト ∪ 選択中ファイルに付いているジャンル（マスターの順序を優先）
            var candidates = new List<string>();
            foreach (var genre in ZipGenre.GetMasterGenres())
            {
                if (!candidates.Contains(genre)) candidates.Add(genre);
            }
            foreach (var info in infos)
            {
                var genres = info.GenreArray;
                if (genres == null) continue;
                foreach (var genre in genres)
                {
                    if (!candidates.Contains(genre)) candidates.Add(genre);
                }
            }

            // 初期状態: 全ファイルに付いている→チェック / どれにも付いていない→なし / 一部→中間
            var initialStates = new Dictionary<string, CheckState>();
            foreach (var genre in candidates)
            {
                var hasCount = infos.Count(info => info.GenreArray != null && info.GenreArray.Contains(genre));
                initialStates[genre] = hasCount == 0 ? CheckState.Unchecked :
                    hasCount == infos.Count ? CheckState.Checked : CheckState.Indeterminate;
            }

            Dictionary<string, CheckState> resultStates;
            string[] allGenres;
            AllowFullpower = false;
            using (var form = new GenreEditForm(candidates, initialStates))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    AllowFullpower = true;
                    return;
                }
                resultStates = form.ResultStates;
                allGenres = form.AllGenres;
            }
            AllowFullpower = true;

            if (resultStates == null || allGenres == null) return;

            // 候補（自由入力で追加された分を含む）をマスターリストへ保存
            ZipGenre.SaveMasterGenres(allGenres);

            var newPaths = new string[paths.Count];
            var anyChange = false;
            for (var i = 0; i < paths.Count; i++)
            {
                var info = infos[i];
                var current = info.GenreArray;
                var result = new List<string>();
                foreach (var genre in allGenres)
                {
                    CheckState state;
                    if (!resultStates.TryGetValue(genre, out state)) continue;
                    var has = current != null && current.Contains(genre);
                    if (state == CheckState.Checked || state == CheckState.Indeterminate && has)
                    {
                        if (!result.Contains(genre)) result.Add(genre);
                    }
                }
                info.GenreArray = result.Count == 0 ? null : result.ToArray();
                var name = names[i];
                var isDir = !string.IsNullOrEmpty(name) && name.Last() == Path.DirectorySeparatorChar;
                newPaths[i] = info.GetPathOfCurrentInfo(isDir);
                if (newPaths[i] != paths[i]) anyChange = true;
            }

            if (anyChange)
            {
                RenameShownArchive(indices.ToArray(), paths.ToArray(), newPaths, atFirstArrayChange: true);
            }
        }
    }
}
