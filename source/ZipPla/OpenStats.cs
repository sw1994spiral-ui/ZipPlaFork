using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace ZipPla
{
    /// <summary>
    /// 本を開いた回数と最後に開いた日時を記録するストア（追加機能）。
    /// キーは「{zpi$...} マーカーを除いたフルパス」なので、ジャンル同期などで
    /// マーカー内（g= 等）だけが書き換わっても記録が途切れない。
    /// 保存先は exe と同じフォルダの openstats.xml。
    /// </summary>
    internal static class OpenStats
    {
        internal sealed class Entry
        {
            public long Size;
            public int Count;
            public DateTime LastOpened;
        }

        private static readonly object Locker = new object();
        private static Dictionary<string, Entry> Table; // null = 未ロード
        private static string LastRecordedKey;
        private static DateTime LastRecordedAt;

        private static string StorePath
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "openstats.xml"); }
        }

        public static string MakeKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string p;
            try { p = Path.GetFullPath(path); } catch { p = path; }
            var dir = Path.GetDirectoryName(p);
            var name = Path.GetFileName(p);
            if (name == null) return null;
            // {zpi$...} マーカーを除去（ジャンル同期はこの中だけを書き換える）
            name = Regex.Replace(name, @"\{zpi\$[^}]*\}", "");
            // マーカー除去で残った「拡張子の前の空白」を詰め、無印状態とキーを一致させる
            name = Regex.Replace(name, @"\s+(\.[^.\\]+)$", "$1").Trim();
            var combined = dir == null ? name : Path.Combine(dir, name);
            return combined.ToLowerInvariant();
        }

        private static Dictionary<string, Entry> Load()
        {
            var table = new Dictionary<string, Entry>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(StorePath))
                {
                    var root = XElement.Load(StorePath);
                    foreach (var el in root.Elements("Item"))
                    {
                        var key = (string)el.Attribute("Key");
                        if (string.IsNullOrEmpty(key)) continue;
                        var e = new Entry();
                        long size;
                        e.Size = long.TryParse((string)el.Attribute("Size"), out size) ? size : -1;
                        int count;
                        e.Count = int.TryParse((string)el.Attribute("Count"), out count) ? count : 0;
                        DateTime last;
                        e.LastOpened = DateTime.TryParse((string)el.Attribute("Last"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out last) ? last : DateTime.MinValue;
                        table[key] = e;
                    }
                }
            }
            catch { }
            return table;
        }

        private static void Save(Dictionary<string, Entry> table)
        {
            try
            {
                var root = new XElement("OpenStats");
                foreach (var kv in table)
                {
                    root.Add(new XElement("Item",
                        new XAttribute("Key", kv.Key),
                        new XAttribute("Size", kv.Value.Size),
                        new XAttribute("Count", kv.Value.Count),
                        new XAttribute("Last", kv.Value.LastOpened.ToString("o", CultureInfo.InvariantCulture))));
                }
                var tmp = StorePath + ".tmp";
                root.Save(tmp);
                if (File.Exists(StorePath)) File.Replace(tmp, StorePath, null);
                else File.Move(tmp, StorePath);
            }
            catch { }
        }

        /// <summary>本を開いたときに呼ぶ。短時間の二重呼び出しは無視する。</summary>
        public static void RecordOpen(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                var isFile = File.Exists(path);
                if (!isFile && !Directory.Exists(path)) return;
                var key = MakeKey(path);
                if (key == null) return;
                lock (Locker)
                {
                    var now = DateTime.Now;
                    if (key == LastRecordedKey && (now - LastRecordedAt).TotalSeconds < 5) return;
                    LastRecordedKey = key;
                    LastRecordedAt = now;
                    if (Table == null) Table = Load();
                    Entry e;
                    if (!Table.TryGetValue(key, out e))
                    {
                        e = new Entry();
                        Table[key] = e;
                    }
                    e.Count++;
                    e.LastOpened = now;
                    try { e.Size = isFile ? new FileInfo(path).Length : -1; } catch { }
                    Save(Table);
                }
            }
            catch { }
        }

        public static int GetCount(string path)
        {
            var e = Find(path);
            return e == null ? 0 : e.Count;
        }

        public static DateTime GetLastOpened(string path)
        {
            var e = Find(path);
            return e == null ? DateTime.MinValue : e.LastOpened;
        }

        private static Entry Find(string path)
        {
            try
            {
                var key = MakeKey(path);
                if (key == null) return null;
                lock (Locker)
                {
                    if (Table == null) Table = Load();
                    Entry e;
                    return Table.TryGetValue(key, out e) ? e : null;
                }
            }
            catch { return null; }
        }
    }
}
