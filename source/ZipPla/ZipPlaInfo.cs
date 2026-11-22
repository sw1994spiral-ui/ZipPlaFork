using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZipPla
{
    class ZipPlaInfo
    {
        public bool CoverInfoForArchive = true;

        // Length >= 1 であること
        private static readonly ZipPlaInfoParam[] zipPlaInfoParamsPrototype = new ZipPlaInfoParam[]
        {
            //new ZipPlaInfoParam("c", @"(\d{1,9})(\.\d+)?", // \d は全角の数字なども含む
            new ZipPlaInfoParam("c", @"([0-9]{1,9})(\.[0-9]+)?",
                (s)=>Tuple.Create(int.Parse(s[0]), string.IsNullOrEmpty(s[1])?0.0:double.Parse("0" + s[1])),
                (o)=>
                {
                    var t = o as Tuple<int, double>;
                    var v1 = t.Item1 < 0 ? 0 : t.Item1;
                    var v2 = t.Item2 < 0 ? 0.0 : t.Item2 > 0.999 ? 0.999 : t.Item2;
                    var result = v1 + v2.ToString("F3").Substring(1);
                    while(result.EndsWith("0")) result = result.Substring(0, result.Length - 1);
                    if(result.EndsWith(".")) result = result.Substring(0, result.Length - 1);
                    return result;
                }),
            new ZipPlaInfoParam("b", @"(r|l|n)?",
                (s)=> { var s0 = s[0].ToLower(); return s0 == "l" ? BindingMode.LeftToRight : s0 == "r" ? BindingMode.RightToLeft : BindingMode.SinglePage; },
                (o)=> {var b = (BindingMode)o; return b == BindingMode.LeftToRight ? "l" : b == BindingMode.RightToLeft ? "r" : "n"; }),
            new ZipPlaInfoParam("r", @"([1-5])",
                (s)=>int.Parse(s[0]),
                (o)=>o.ToString()),
            new ZipPlaInfoParam("t", @"((?:[^,]+,)*[^,]+,?)",
                //(s)=>(from ss in s[0].Split(new char[1] {','}, StringSplitOptions.RemoveEmptyEntries) select ss.Trim()).OrderBy(entry => entry, new LogicalStringComparer()).ToArray(),
                //(o)=>string.Join(",", (o as string[]).OrderBy(entry => entry, new LogicalStringComparer()).ToArray()))
                (s)=>(from ss in s[0].Split(new char[1] {','}, StringSplitOptions.RemoveEmptyEntries) select ss.Trim()).ToArray(),
                (o)=>string.Join(",", o as string[])),
            new ZipPlaInfoParam("d", @"(l|r)?",
                (s)=>s[0].ToLower() == "r" ? BindingMode.LeftToRight : BindingMode.RightToLeft,
                (o)=>((BindingMode)o) == BindingMode.LeftToRight ? "r" : "l"),
            // ジャンル専用機能（試作）: タグ "t" とは別枠の "g" キー。タグと同じカンマ区切り形式。
            // 末尾に追加して既存パラメータのインデックス（0:c, 1:b, 2:r, 3:t, 4:d）を変えないこと。
            new ZipPlaInfoParam("g", @"((?:[^,]+,)*[^,]+,?)",
                (s)=>(from ss in s[0].Split(new char[1] {','}, StringSplitOptions.RemoveEmptyEntries) select ss.Trim()).ToArray(),
                (o)=>string.Join(",", o as string[])),
        };

        private void FixLeftToRightInfo()
        {
            if (zipPlaInfoParams[4].Value is bool v)
            {
                var p = zipPlaInfoParams[1];
                if(p.Value as bool? == null) p.Value = v;
                zipPlaInfoParams[4].Value = null;
            }
        }

        private readonly ZipPlaInfoParam[] zipPlaInfoParams;
        public Tuple<int, double> ThumbnailInfo
        {
            get => zipPlaInfoParams[0].Value as Tuple<int, double>;
            set { zipPlaInfoParams[0].Value = value; FixLeftToRightInfo(); }
        }
        public BindingMode? BindingModeForSet
        {
            get => zipPlaInfoParams[1].Value as BindingMode? ?? zipPlaInfoParams[4].Value as BindingMode?;
            set
            {
                zipPlaInfoParams[1].Value = value;
                zipPlaInfoParams[4].Value = null;
            }
        }
        public int? Rating
        {
            get => zipPlaInfoParams[2].Value as int?;
            set { zipPlaInfoParams[2].Value = (value == null || (0 < value && value <= 5)) ? value : null; FixLeftToRightInfo(); }
        }
        public string[] TagArray
        {
            get => zipPlaInfoParams[3].Value as string[];
            set { zipPlaInfoParams[3].Value = value; FixLeftToRightInfo(); }
        }
        // ジャンル専用機能（試作）: "g" キーの値。タグとは独立に読み書きされる。
        public string[] GenreArray
        {
            get => zipPlaInfoParams[5].Value as string[];
            set { zipPlaInfoParams[5].Value = value; }
        }
        private readonly Tuple<int, int>[] indexLengthPairs;
        private readonly string path;
        public Tuple<int,int,string>[] GetIndexLengthTagTorios()
        {
#if AUTOBUILD
            try
#endif
            {
                var tags = TagArray;
                if (tags == null) return new Tuple<int, int, string>[0];
                var tagsCount = tags.Length;
                if (tagsCount == 0) return new Tuple<int, int, string>[0];
                var result = new Tuple<int, int, string>[tagsCount];
                var pair = indexLengthPairs[3];
                var index0 = pair.Item1 + 2;
                var pathLength = path.Length;
                while (index0 < pathLength && path[index0] == ' ') index0++;
                var fullLength = pair.Item2 - 2;
                var targetStr = path.Substring(index0, fullLength);
                //if(targetStr.Length < fullLength)
                //{
                //    System.Windows.Forms.MessageBox.Show($"{targetStr}");
                //}
                var charArray = targetStr.ToArray();
                var j = 0;
                int index = 0;
                int index0FullLength = index0 + fullLength;
                for (var i = 0; i < fullLength; i++)
                {
                    if (charArray[i] == ',')
                    {
                        var stop = i;
                        while (stop > 0 && charArray[stop - 1] == ' ') stop--;
                        var length = stop - index;
                        if (length > 0)
                        {
                            result[j++] = new Tuple<int, int, string>(index + index0, length, targetStr.Substring(index, length));
                        }
                        while (i + 1 < fullLength && charArray[i + 1] == ' ') i++;
                        index = i + 1;
                    }
                }
                while (fullLength > 0 && charArray[fullLength - 1] == ' ') fullLength--;
                var len = fullLength - index;
                if (len > 0)
                {
                    result[j++] = new Tuple<int, int, string>(index + index0, len, targetStr.Substring(index, len));
                }
                return result;
            }
#if AUTOBUILD
            catch
            {
                return new Tuple<int, int, string>[0];
            }
#endif
        }

        private static readonly Regex canBeTagRegex = new Regex(@"^(?:[^\s,;\\/:*?""<>{}|]|[^\s,;\\/:*?""<>{}|][^\t,;\\/:*?""<>{}|]*[^\s,;\\/:*?""<>{}|])$", RegexOptions.Compiled);
        public static bool CanBeTag(string tag)
        {
            return canBeTagRegex.IsMatch(tag);
        }
        public static bool CanBeTag(IEnumerable<string> tags)
        {
            return tags.All(tag => canBeTagRegex.IsMatch(tag));
        }
        
        //private static Regex getOnlyRatingRegex = new Regex(@"\{zpi\$(?:r|[^}]*;r)=([1-5]).*\}[^\\\/]*[\\\/]?$", RegexOptions.Compiled);
        private static readonly Regex getOnlyRatingRegex = new Regex(@"\{zpi\$\s*(?:r|[^}\\\/]*;\s*r)\s*=\s*([1-5])\s*(?:;[^}\\\/]*)?\}[^\\\/]*[\\\/]?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // GetOnlyRating は文字列に対する純粋関数なので結果をメモ化する（最適化実験）。
        // 大量ファイルのフォルダでは並び替え・絞り込み・一覧表示のたびに全件へ正規表現を
        // 適用していたため、2回目以降は辞書参照で済ませる。結果は従来と完全に同一。
        private static readonly Dictionary<string, int> onlyRatingCache = new Dictionary<string, int>();
        private static readonly object onlyRatingCacheLocker = new object();
        public static int GetOnlyRating(string path)
        {
            if (path != null)
            {
                lock (onlyRatingCacheLocker)
                {
                    int cached;
                    if (onlyRatingCache.TryGetValue(path, out cached)) return cached;
                }
            }
            var m = getOnlyRatingRegex.Match(path);
            var result = m.Success ? int.Parse(m.Groups[1].Value) : -1;
            if (path != null)
            {
                lock (onlyRatingCacheLocker)
                {
                    if (onlyRatingCache.Count > 100000) onlyRatingCache.Clear();
                    onlyRatingCache[path] = result;
                }
            }
            return result;
        }

        /// <summary>
        /// ランダムソートで使用することを想定し正確さよりも速さを優先
        /// </summary>
        public static string GetBasePathRoughly(string path)
        {
            if (path == null) return null;
            var start = path.IndexOf("{zpi$", StringComparison.OrdinalIgnoreCase);
            if (start < 0) return path;
            var startEnd = start + 5;
            if (path.Length <= startEnd) return path;
            if (path.IndexOf('\\', startEnd) >= 0) return path;
            var stop = path.IndexOf('}', startEnd);
            if (stop < 0) return path;
            var stopEnd = stop + 1;
            if (start > 0 && path[start - 1] == ' ') start--;
            return path.Substring(0, start) + path.Substring(stopEnd);
        }

        private static readonly Regex getOnlyPageSequenceFromFullNameRegex = new Regex(@"\{zpi\$\s*(?:b|[^}\\\/]*;\s*b)\s*=\s*(l|r|n)\s*(?:;[^}\\\/]*)?\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static BindingMode? GetOnlyBindingModeFromFullName(string fullName)
        {
            var m = getOnlyPageSequenceFromFullNameRegex.Matches(fullName);
            if (m.Count > 0)
            {
                switch(m[m.Count - 1].Groups[1].Value.ToLower())
                {
                    case "l": return BindingMode.LeftToRight;
                    case "r": return BindingMode.RightToLeft;
                    default: return BindingMode.SinglePage;
                }
            }
            else
            {
                return null;
            }
        }

        private static readonly Regex fileNameRegex;


        static ZipPlaInfo()
        {
            var pettern0 = "(?:";
            foreach (var info in zipPlaInfoParamsPrototype)
            {
                pettern0 += @"(?:" + info.MnemonicPatternPair + ")|";
            }
            pettern0 = pettern0.Substring(0, pettern0.Length - 1) + @")";
            //var fileNameRegexPattern = @"\s*\{\s*zpi\s*\$\s*((?:" + pettern0 + ";)*" + pettern0 + @")?;?\s*\}\s*"; // 前後の空白を吸収
            var fileNameRegexPattern = @"\s?\{\s*zpi\s*\$\s*((?:" + pettern0 + ";)*" + pettern0 + @")?;?\s*\}"; // 頭の空白を最大一つだけ吸収
            fileNameRegex = new Regex(fileNameRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public ZipPlaInfo(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }
            this.path = path;
            
            zipPlaInfoParams = new ZipPlaInfoParam[zipPlaInfoParamsPrototype.Length];
            indexLengthPairs = new Tuple<int, int>[zipPlaInfoParamsPrototype.Length];
            for (var i = 0; i < zipPlaInfoParamsPrototype.Length; i++)
            {
                zipPlaInfoParams[i] = zipPlaInfoParamsPrototype[i].Clone() as ZipPlaInfoParam;
            }

            //if (path.EndsWith(Path.DirectorySeparatorChar.ToString())) path = path.Substring(0, path.Length - 1);
            //var m = fileNameRegex.Match(Path.GetFileName(path));
            var pathArray = path.ToArray();
            int sepPos;
            for (sepPos = pathArray.Length - 2; sepPos >= 0; sepPos--) { var c = pathArray[sepPos]; if (c == '\\' || c == '/') break; }
            sepPos++;
            var m = fileNameRegex.Match(sepPos > 0 ? path.Substring(sepPos) : path);
            if (m.Success)
            {
                var index = m.Groups[1].Index;
                foreach (var paramString in m.Groups[1].Value.Split(new char[1] { ';' }, StringSplitOptions.None))
                {
                    for(var i = 0; i < zipPlaInfoParamsPrototype.Length; i++)
                    {
                        var info = zipPlaInfoParams[i];
                        var m2 = info.Regex.Match(paramString);
                        if (m2.Success)
                        {
                            info.SetValue(m2.Groups);
                            indexLengthPairs[i] = Tuple.Create(index + sepPos, paramString.Length);
                            break;
                        }
                    }
                    index += paramString.Length + 1;
                }
            }

        }

        public string GetPathOfCurrentInfo(bool isDir)
        {
            string basePrefix;
            string baseSuffix;
            var dirName = Path.GetDirectoryName(path);
            if (dirName != null) // ルートなどだと null になる
            {
                if (isDir)
                {
                    basePrefix = Path.Combine(dirName, fileNameRegex.Replace(Path.GetFileName(path), ""));
                    baseSuffix = "";
                }
                else
                {
                    basePrefix = Path.Combine(dirName, fileNameRegex.Replace(Path.GetFileNameWithoutExtension(path), ""));
                    baseSuffix = Path.GetExtension(path);
                }
            }
            else
            {
                basePrefix = fileNameRegex.Replace(path, "");
                baseSuffix = "";
            }
            
            var infoString = GetInfoString();
            if (string.IsNullOrEmpty(infoString))
            {
                return basePrefix + baseSuffix;
            }
            else
            {
                return basePrefix + " " + infoString + baseSuffix;
            }
        }

        private string GetInfoString()
        {
            var result = "{zpi$";
            var existsParam = false;
            foreach (var info in zipPlaInfoParams)
            {
                if (info.Value != null)
                {
                    existsParam = true;
                    result += info.GetInfoString() + ";";
                }
            }
            if (!existsParam)
            {
                return null;
            }
            return result.Substring(0, result.Length - 1) + "}";
        }

        private delegate object Parse(string[] str);
        private delegate string DeParse(object obj);

        private class ZipPlaInfoParam : ICloneable
        {
            public readonly string Mnemonic;
            public readonly string Pattern;
            public readonly string MnemonicPatternPair;
            public readonly Regex Regex;
            private readonly Parse Parse;
            private readonly DeParse DeParse;
            public object Value;
            public ZipPlaInfoParam(string mnemonic, string pattern, Parse parse, DeParse deparse)
            {
                Mnemonic = mnemonic;
                Pattern = pattern;
                Parse = parse;
                DeParse = deparse;
                MnemonicPatternPair = @"\s*" + mnemonic + @"\s*=\s*" + pattern + @"\s*";
                // static readonly ではないので Compiled は不適切に見えるが、このコンストラクタは static readonly な ZipPlaInfo.zipPlaInfoParamsPrototype の長さ分しか呼び出されないので問題ない
                Regex = new Regex("^" + MnemonicPatternPair + "$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                Value = null;
            }
            public ZipPlaInfoParam(string mnemonic, string pattern, string mnemonicPatternPair, Regex regex, Parse parse, DeParse deparse)
            {
                Mnemonic = mnemonic;
                Pattern = pattern;
                Parse = parse;
                DeParse = deparse;
                MnemonicPatternPair = mnemonicPatternPair;
                Regex = regex;
                Value = null;
            }

            public void SetValue(GroupCollection g)
            {
                var str = new string[g.Count - 1];
                for (var i = 0; i < str.Length; i++)
                {
                    str[i] = g[i + 1].Value;
                }
                Value = Parse(str);
            }

            public string GetInfoString()
            {
                return Mnemonic + "=" + DeParse(Value);
            }

            public object Clone()
            {
                return new ZipPlaInfoParam(Mnemonic, Pattern, MnemonicPatternPair, Regex, Parse, DeParse);
            }
        }
    }

    /// <summary>
    /// カタログキャッシュ（最適化実験）。
    /// フォルダを開くたびに再計算される「ファイル名マーカーの解析結果（評価・タグ・ジャンル）」と
    /// 「アーカイブのページ数」を、exe 横の catalogcache フォルダにフォルダ単位で保存する。
    /// 列挙で得た各ファイルのサイズ・更新日時がキャッシュと全件一致した場合だけ再利用し、
    /// 1件でも不整合があればそのフォルダ分は丸ごと再構築する。
    /// キャッシュファイルが壊れている場合は通常の読み込みにフォールバックする。
    /// GeneralConfig.CatalogCache が false の場合は一切動作しない。
    /// </summary>
    internal static class CatalogCache
    {
        public sealed class Entry
        {
            public long Size;
            public long MtimeUtcTicks;
            public int Rating;      // ZipPlaInfo.GetOnlyRating の戻り値（-1 = 評価なし）
            public string[] Tags;   // null = タグなし
            public string[] Genres; // null = ジャンルなし。ジャンルメニューのスキャンと同じ正規表現で解析した分割結果
            public int PageCount;   // -1 = 未取得
        }

        private const int CacheFileMagic = 0x5A504331; // "ZPC1"
        private const int CacheFileVersion = 1;
        private const int MaxCacheFiles = 64;

        private static readonly object Locker = new object();
        private static bool? enabledCache;
        private static string activeFolderPath;
        private static Dictionary<string, Entry> activeTable; // キーはフルパス
        private static bool activeDirty;
        private static DateTime lastFlushUtc = DateTime.MinValue;

        // CatalogForm.Genre のジャンル収集と同じ正規表現（結果を完全一致させるため）
        private static readonly System.Text.RegularExpressions.Regex genreScanRegex =
            new System.Text.RegularExpressions.Regex(@"\{zpi\$[^}]*?g=([^;}]+)");

        public static bool Enabled
        {
            get
            {
                if (enabledCache == null)
                {
                    var value = true;
                    try { value = new GeneralConfig().CatalogCache; } catch { }
                    enabledCache = value;
                }
                return enabledCache.Value;
            }
        }

        private static string CacheDirectory
        {
            get { return Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "catalogcache"); }
        }

        private static string GetCacheFilePath(string normalizedFolderPath)
        {
            byte[] hash;
            using (var sha = System.Security.Cryptography.SHA1.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedFolderPath.ToLowerInvariant()));
            }
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return Path.Combine(CacheDirectory, sb.ToString() + ".dat");
        }

        private static string GetRelativeKey(string normalizedFolderPath, string fullPath)
        {
            var prefix = normalizedFolderPath.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? normalizedFolderPath : normalizedFolderPath + Path.DirectorySeparatorChar;
            if (fullPath != null && fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(prefix.Length);
            }
            return fullPath;
        }

        /// <summary>
        /// フォルダを開く準備としてキャッシュを検証し、アクティブ化する。
        /// 検証が通れば保存されていたテーブルを、通らなければ再構築して保存したテーブルを返す。
        /// 無効化されている場合や対象外の場合は null を返し、アクティブな状態も解除する。
        /// bwMakePreviewPrepare のバックグラウンドスレッドから呼ばれる。
        /// </summary>
        public static Dictionary<string, Entry> Prepare(string dirPath, FileSystemInfo[] fileInfos)
        {
            lock (Locker)
            {
                FlushLocked();
                activeFolderPath = null;
                activeTable = null;
                activeDirty = false;
                if (!Enabled || dirPath == null || fileInfos == null) return null;
                string normalized;
                try { normalized = Path.GetFullPath(dirPath); } catch { return null; }
                var table = LoadAndValidate(normalized, fileInfos);
                if (table == null)
                {
                    table = Build(normalized, fileInfos);
                    if (table == null) return null;
                    SaveLocked(normalized, table);
                }
                activeFolderPath = normalized;
                activeTable = table;
                return table;
            }
        }

        public static void Deactivate()
        {
            lock (Locker)
            {
                FlushLocked();
                activeFolderPath = null;
                activeTable = null;
                activeDirty = false;
            }
        }

        public static void Flush()
        {
            lock (Locker) { FlushLocked(); }
        }

        public static bool TryGetRating(string fullPath, out int rating)
        {
            rating = -1;
            Entry entry;
            if (!TryGetEntry(fullPath, out entry)) return false;
            rating = entry.Rating;
            return true;
        }

        public static bool TryGetGenres(string fullPath, out string[] genres)
        {
            genres = null;
            Entry entry;
            if (!TryGetEntry(fullPath, out entry)) return false;
            genres = entry.Genres;
            return true;
        }

        public static bool TryGetTags(string fullPath, out string[] tags)
        {
            tags = null;
            Entry entry;
            if (!TryGetEntry(fullPath, out entry)) return false;
            tags = entry.Tags;
            return true;
        }

        public static bool TryGetEntry(string fullPath, out Entry entry)
        {
            entry = null;
            if (fullPath == null) return false;
            lock (Locker)
            {
                if (activeTable == null) return false;
                return activeTable.TryGetValue(fullPath, out entry);
            }
        }

        /// <summary>
        /// サムネイル読み込み等で判明したページ数をキャッシュへ反映する。
        /// アクティブなフォルダに属するファイルだけが対象。保存は間引いて行う。
        /// </summary>
        public static void NotePageCount(string fullPath, int pageCount)
        {
            if (fullPath == null || pageCount < 0) return;
            lock (Locker)
            {
                if (activeTable == null) return;
                Entry entry;
                if (!activeTable.TryGetValue(fullPath, out entry)) return;
                if (entry.PageCount == pageCount) return;
                try
                {
                    var fi = new FileInfo(fullPath);
                    if (!fi.Exists) return;
                    entry.Size = fi.Length;
                    entry.MtimeUtcTicks = fi.LastWriteTimeUtc.Ticks;
                }
                catch { return; }
                entry.PageCount = pageCount;
                activeDirty = true;
                if ((DateTime.UtcNow - lastFlushUtc).TotalSeconds >= 30) FlushLocked();
            }
        }

        private static Dictionary<string, Entry> LoadAndValidate(string normalizedFolderPath, FileSystemInfo[] fileInfos)
        {
            try
            {
                var filePath = GetCacheFilePath(normalizedFolderPath);
                if (!File.Exists(filePath)) return null;
                Dictionary<string, Entry> byRelativeKey;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var br = new BinaryReader(fs, Encoding.UTF8))
                {
                    if (br.ReadInt32() != CacheFileMagic) return null;
                    if (br.ReadInt32() != CacheFileVersion) return null;
                    var storedFolder = br.ReadString();
                    if (!string.Equals(storedFolder, normalizedFolderPath, StringComparison.OrdinalIgnoreCase)) return null;
                    var count = br.ReadInt32();
                    if (count < 0 || count != fileInfos.Length) return null;
                    byRelativeKey = new Dictionary<string, Entry>(count, StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < count; i++)
                    {
                        var relKey = br.ReadString();
                        var entry = new Entry();
                        entry.Size = br.ReadInt64();
                        entry.MtimeUtcTicks = br.ReadInt64();
                        entry.Rating = br.ReadInt32();
                        entry.Tags = ReadStringArray(br);
                        entry.Genres = ReadStringArray(br);
                        entry.PageCount = br.ReadInt32();
                        byRelativeKey[relKey] = entry;
                    }
                }
                var table = new Dictionary<string, Entry>(fileInfos.Length, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < fileInfos.Length; i++)
                {
                    var fileInfo = fileInfos[i];
                    var fullPath = fileInfo.FullName;
                    Entry entry;
                    if (!byRelativeKey.TryGetValue(GetRelativeKey(normalizedFolderPath, fullPath), out entry)) return null;
                    var entity = FileInfoEx.GetEntityFileInfo(fileInfo);
                    if (entity == null) return null;
                    if (entry.Size != entity.Length) return null;
                    if (entry.MtimeUtcTicks != entity.LastWriteTimeUtc.Ticks) return null;
                    table[fullPath] = entry;
                }
                return table;
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, Entry> Build(string normalizedFolderPath, FileSystemInfo[] fileInfos)
        {
            try
            {
                var table = new Dictionary<string, Entry>(fileInfos.Length, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < fileInfos.Length; i++)
                {
                    var fileInfo = fileInfos[i];
                    var entity = FileInfoEx.GetEntityFileInfo(fileInfo);
                    if (entity == null) continue;
                    var entry = new Entry();
                    entry.Size = entity.Length;
                    entry.MtimeUtcTicks = entity.LastWriteTimeUtc.Ticks;
                    entry.Rating = ZipPlaInfo.GetOnlyRating(fileInfo.Name);
                    entry.Genres = ParseGenres(fileInfo.Name);
                    entry.Tags = ParseTags(fileInfo.FullName);
                    entry.PageCount = -1;
                    table[fileInfo.FullName] = entry;
                }
                return table;
            }
            catch
            {
                return null;
            }
        }

        private static string[] ParseGenres(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            var match = genreScanRegex.Match(fileName);
            if (!match.Success) return null;
            var parts = match.Groups[1].Value.Split(',');
            var result = new List<string>();
            for (var i = 0; i < parts.Length; i++)
            {
                var genre = parts[i].Trim();
                if (genre.Length > 0) result.Add(genre);
            }
            return result.Count > 0 ? result.ToArray() : null;
        }

        private static string[] ParseTags(string fullPath)
        {
            try
            {
                var info = new ZipPlaInfo(fullPath);
                return info.TagArray;
            }
            catch
            {
                return null;
            }
        }

        private static void FlushLocked()
        {
            if (!activeDirty || activeFolderPath == null || activeTable == null) return;
            SaveLocked(activeFolderPath, activeTable);
            activeDirty = false;
            lastFlushUtc = DateTime.UtcNow;
        }

        private static void SaveLocked(string normalizedFolderPath, Dictionary<string, Entry> table)
        {
            try
            {
                var dir = CacheDirectory;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var filePath = GetCacheFilePath(normalizedFolderPath);
                var tmpPath = filePath + ".tmp";
                using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs, Encoding.UTF8))
                {
                    bw.Write(CacheFileMagic);
                    bw.Write(CacheFileVersion);
                    bw.Write(normalizedFolderPath);
                    bw.Write(table.Count);
                    foreach (var pair in table)
                    {
                        var entry = pair.Value;
                        bw.Write(GetRelativeKey(normalizedFolderPath, pair.Key));
                        bw.Write(entry.Size);
                        bw.Write(entry.MtimeUtcTicks);
                        bw.Write(entry.Rating);
                        WriteStringArray(bw, entry.Tags);
                        WriteStringArray(bw, entry.Genres);
                        bw.Write(entry.PageCount);
                    }
                }
                if (File.Exists(filePath)) File.Replace(tmpPath, filePath, null);
                else File.Move(tmpPath, filePath);
                PruneCacheFiles(dir);
            }
            catch { }
        }

        private static void PruneCacheFiles(string dir)
        {
            try
            {
                var files = new DirectoryInfo(dir).GetFiles("*.dat");
                if (files.Length <= MaxCacheFiles) return;
                var ordered = files.OrderBy(f => f.LastWriteTimeUtc).ToArray();
                for (var i = 0; i < ordered.Length - MaxCacheFiles; i++)
                {
                    try { ordered[i].Delete(); } catch { }
                }
            }
            catch { }
        }

        private static void WriteStringArray(BinaryWriter bw, string[] array)
        {
            if (array == null)
            {
                bw.Write(-1);
                return;
            }
            bw.Write(array.Length);
            for (var i = 0; i < array.Length; i++) bw.Write(array[i] ?? "");
        }

        private static string[] ReadStringArray(BinaryReader br)
        {
            var length = br.ReadInt32();
            if (length < 0) return null;
            var result = new string[length];
            for (var i = 0; i < length; i++) result[i] = br.ReadString();
            return result;
        }
    }
}
