using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace ZipPla
{
    /// <summary>
    /// 追加機能（複数ジャンル絞り込み・ジャンル名一括変更・重複チェック・設定の書き出し/読み込み）の
    /// CatalogForm 側コード。ダイアログ本体は ZipPlaExtras.cs にある。
    /// CatalogForm 本体への変更はコンストラクタでの SetupExtrasMenu() 呼び出し1か所のみ。
    /// </summary>
    public partial class CatalogForm
    {
        /// <summary>メインメニューに「ツール」メニューを追加する。コンストラクタから1回だけ呼ぶ。</summary>
        private void SetupExtrasMenu()
        {
            var menu = new ToolStripMenuItem("ツール");
            var dupItem = new ToolStripMenuItem("重複ファイルをチェック...");
            dupItem.Click += (s, ev) => ShowDuplicateCheck();
            var exportItem = new ToolStripMenuItem("設定とブックマークを書き出し...");
            exportItem.Click += (s, ev) => ExportConfig();
            var importItem = new ToolStripMenuItem("設定とブックマークを読み込み...");
            importItem.Click += (s, ev) => ImportConfig();
            menu.DropDownItems.Add(dupItem);
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add(exportItem);
            menu.DropDownItems.Add(importItem);
            menuStrip.Items.Add(menu);
        }

        /// <summary>ジャンル候補（マスターリスト ∪ 現在のカタログ）とカタログ内件数の一覧を作る。</summary>
        private List<KeyValuePair<string, int>> GetGenreCandidatesWithCounts()
        {
            var counts = CollectCatalogGenreCounts();
            var result = new List<KeyValuePair<string, int>>();
            var seen = new HashSet<string>();
            foreach (var genre in ZipGenre.GetMasterGenres())
            {
                if (!seen.Add(genre)) continue;
                int count;
                counts.TryGetValue(genre, out count);
                result.Add(new KeyValuePair<string, int>(genre, count));
            }
            foreach (var genre in CollectCatalogGenres())
            {
                if (!seen.Add(genre)) continue;
                int count;
                counts.TryGetValue(genre, out count);
                result.Add(new KeyValuePair<string, int>(genre, count));
            }
            return result;
        }

        /// <summary>「ジャンル」メニュー →「複数ジャンルで絞り込み...」。</summary>
        private void ShowGenreMultiFilter()
        {
            var candidates = GetGenreCandidatesWithCounts();
            if (candidates.Count == 0)
            {
                MessageBox.Show(this, "ジャンルはまだありません。", null, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string[] selected;
            bool append;
            AllowFullpower = false;
            using (var form = new GenreMultiFilterForm(candidates))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    AllowFullpower = true;
                    return;
                }
                selected = form.SelectedGenres;
                append = form.Append;
            }
            AllowFullpower = true;
            if (selected == null || selected.Length == 0) return;

            var terms = selected.Select(genre => ZipGenre.GetFilterTerm(genre)).ToArray();
            if (append)
            {
                foreach (var term in terms) filterAdd(term, weak: true);
            }
            else
            {
                cbFilter.Text = string.Join(" ", terms);
                cbFilter.SelectAll();
            }
        }

        /// <summary>
        /// 「ジャンル」メニュー →「ジャンル名の一括変更...」。
        /// 現在のカタログ全ファイルの g= と、マスターリスト内の名前を置き換える。
        /// リネームはジャンル編集と同じ RenameShownArchive に任せる。
        /// </summary>
        private void ShowGenreBulkRename()
        {
            if (CreatingZipPathArray) return;
            var candidates = GetGenreCandidatesWithCounts();
            if (candidates.Count == 0)
            {
                MessageBox.Show(this, "ジャンルはまだありません。", null, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string oldGenre, newGenre;
            AllowFullpower = false;
            using (var form = new GenreBulkRenameForm(candidates))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    AllowFullpower = true;
                    return;
                }
                oldGenre = form.OldGenre;
                newGenre = form.NewGenre;
            }
            AllowFullpower = true;
            if (string.IsNullOrEmpty(oldGenre) || string.IsNullOrEmpty(newGenre)) return;
            if (oldGenre == newGenre) return;
            if (!ZipPlaInfo.CanBeTag(newGenre))
            {
                MessageBox.Show(this, "ジャンル名には次の文字は使えません。\n\n\\ / : * ? \" < > | { } ; ,",
                    null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var indices = new List<int>();
            var paths = new List<string>();
            var newPaths = new List<string>();
            lock (ZipPathArrayLocker)
            {
                var zpa = ZipPathArray;
                var zna = ZipNameArray;
                if (zpa == null || zna == null) return;
                for (var i = 0; i < zpa.Length; i++)
                {
                    var path = zpa[i];
                    if (string.IsNullOrEmpty(path)) continue;
                    var info = new ZipPlaInfo(path);
                    var genres = info.GenreArray;
                    if (genres == null || !genres.Contains(oldGenre)) continue;
                    var replaced = new List<string>();
                    foreach (var genre in genres)
                    {
                        var value = genre == oldGenre ? newGenre : genre;
                        if (!replaced.Contains(value)) replaced.Add(value);
                    }
                    info.GenreArray = replaced.ToArray();
                    var name = i < zna.Length ? zna[i] : null;
                    var isDir = !string.IsNullOrEmpty(name) && name.Last() == Path.DirectorySeparatorChar;
                    var newPath = info.GetPathOfCurrentInfo(isDir);
                    if (newPath == path) continue;
                    indices.Add(i);
                    paths.Add(path);
                    newPaths.Add(newPath);
                }
            }

            // マスターリスト側の名前も置き換える（対象がカタログ外にしかなくても意味があるため常に行う）
            var master = ZipGenre.GetMasterGenres().ToList();
            if (master.Contains(oldGenre))
            {
                if (master.Contains(newGenre))
                {
                    master.Remove(oldGenre);
                }
                else
                {
                    master[master.IndexOf(oldGenre)] = newGenre;
                }
                ZipGenre.SaveMasterGenres(master);
            }

            if (indices.Count > 0)
            {
                RenameShownArchive(indices.ToArray(), paths.ToArray(), newPaths.ToArray(), atFirstArrayChange: true);
            }
            MessageBox.Show(this, $"ジャンル「{oldGenre}」を「{newGenre}」に変更しました（対象ファイル {indices.Count} 件）。",
                null, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 「ツール」メニュー →「重複ファイルをチェック...」。
        /// 現在のカタログのうち実在するファイル/フォルダを対象に、
        /// 「マーカーと拡張子を除いた名前＋サイズ」が一致するものを重複として集める。
        /// </summary>
        private void ShowDuplicateCheck()
        {
            if (CreatingZipPathArray) return;
            string[] zpa;
            string[] zna;
            lock (ZipPathArrayLocker)
            {
                zpa = ZipPathArray;
                zna = ZipNameArray;
            }
            if (zpa == null || zna == null) return;

            var markerRegex = new Regex(@"\{zpi\$[^}]*\}");
            var groupsByKey = new Dictionary<string, DuplicateGroupInfo>();
            var groupOrder = new List<DuplicateGroupInfo>();
            for (var i = 0; i < zpa.Length; i++)
            {
                var path = zpa[i];
                if (string.IsNullOrEmpty(path)) continue;
                if (path.IndexOf(Path.AltDirectorySeparatorChar) >= 0) continue; // 書庫内などの仮想パスは対象外
                var name = i < zna.Length ? zna[i] : null;
                if (string.IsNullOrEmpty(name)) continue;
                var isDir = name.Last() == Path.DirectorySeparatorChar;
                long size;
                DateTime lastWrite;
                try
                {
                    if (isDir)
                    {
                        var dirInfo = new DirectoryInfo(path);
                        if (!dirInfo.Exists) continue;
                        size = -1;
                        lastWrite = dirInfo.LastWriteTime;
                    }
                    else
                    {
                        var fileInfo = new FileInfo(path);
                        if (!fileInfo.Exists) continue;
                        size = fileInfo.Length;
                        lastWrite = fileInfo.LastWriteTime;
                    }
                }
                catch
                {
                    continue;
                }
                var plainName = name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var keyName = markerRegex.Replace(Path.GetFileNameWithoutExtension(plainName), "").Trim();
                if (keyName.Length == 0) continue;
                var key = keyName + "\n" + (isDir ? "dir" : size.ToString());
                DuplicateGroupInfo group;
                if (!groupsByKey.TryGetValue(key, out group))
                {
                    group = new DuplicateGroupInfo { KeyName = keyName };
                    groupsByKey[key] = group;
                    groupOrder.Add(group);
                }
                group.Items.Add(new DuplicateItemInfo
                {
                    Path = path,
                    Name = plainName,
                    Size = size,
                    LastWriteTime = lastWrite,
                    IsDirectory = isDir,
                });
            }

            var groups = groupOrder.Where(g => g.Items.Count >= 2).ToList();
            foreach (var group in groups)
            {
                group.Items.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
            }
            if (groups.Count == 0)
            {
                MessageBox.Show(this, "現在の表示の中に、名前とサイズが一致する重複は見つかりませんでした。",
                    null, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool deletedAny;
            AllowFullpower = false;
            using (var form = new DuplicateCheckForm(groups))
            {
                form.ShowDialog(this);
                deletedAny = form.DeletedAny;
            }
            AllowFullpower = true;

            if (deletedAny)
            {
                // 削除を反映するために現在の場所を開き直す
                var location = zabLocation.Text;
                if (!string.IsNullOrEmpty(location))
                {
                    MoveLocation(ApplicationProviderUser.DragAndDrop, location);
                }
            }
        }

        private static string GetConfigXmlPath()
        {
            return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "config.xml");
        }

        /// <summary>
        /// 「ツール」メニュー →「設定とブックマークを書き出し...」。
        /// 設定・ブックマーク・ジャンル候補はすべて config.xml に保存されているため、そのコピーを書き出す。
        /// </summary>
        private void ExportConfig()
        {
            var configPath = GetConfigXmlPath();
            if (!File.Exists(configPath))
            {
                MessageBox.Show(this, "config.xml が見つかりませんでした。", null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "ZipPla設定ファイル (*.xml)|*.xml";
                dialog.FileName = "ZipPla-config-" + DateTime.Now.ToString("yyyyMMdd") + ".xml";
                dialog.OverwritePrompt = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.Copy(configPath, dialog.FileName, true);
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, "書き出しに失敗しました。\n\n" + e.Message, null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            MessageBox.Show(this, "設定とブックマークを書き出しました。", null, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 「ツール」メニュー →「設定とブックマークを読み込み...」。
        /// 読み込んだ設定は次回起動時に反映される。実行中の ZipPla が終了時に設定を保存して
        /// 上書きしてしまうのを防ぐため、読み込み後は確認のうえプロセスを終了する。
        /// 現在の設定は config.xml.pre-import に退避する。
        /// </summary>
        private void ImportConfig()
        {
            string sourcePath;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "ZipPla設定ファイル (*.xml)|*.xml";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                sourcePath = dialog.FileName;
            }
            try
            {
                var root = XElement.Load(sourcePath);
                if (root.Name.LocalName != "ZipPlaConfig")
                {
                    MessageBox.Show(this, "ZipPla の設定ファイルではないようです。", null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "設定ファイルを読み込めませんでした。\n\n" + e.Message, null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (MessageBox.Show(this, "現在の設定とブックマークを、選んだファイルの内容で置き換えます。\n読み込みを反映するため、ZipPla はいったん終了します（再起動すると反映されます）。\n\nよろしいですか？",
                null, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            var configPath = GetConfigXmlPath();
            try
            {
                if (File.Exists(configPath))
                {
                    File.Copy(configPath, configPath + ".pre-import", true);
                }
                File.Copy(sourcePath, configPath, true);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "読み込みに失敗しました。\n\n" + e.Message, null, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            MessageBox.Show(this, "読み込みました。ZipPla を終了します。再起動すると反映されます。",
                null, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Environment.Exit(0);
        }
    }
}
