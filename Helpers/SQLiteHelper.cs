using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZipImageViewer
{
    internal static class SQLiteHelper
    {
        internal const string dbFileName = @"thumb_database.sqlite";
        internal static string DbFileFullPath => Path.Combine(Setting.ThumbDbDir, dbFileName);
        private readonly static object lock_ThumbDb = new object();

        internal static class Table_ThumbsData
        {
            internal const string Name = @"thumbs_data";
            internal const string Col_VirtualPath = @"virtualPath";
            internal const string Col_DecodeWidth = @"decodeWidth";
            internal const string Col_DecodeHeight = @"decodeHeight";
            internal const string Col_ThumbData = @"thumbData";
        }

        /// <summary>
        /// Returns an array of objects containing the return value from each Func.
        /// </summary>
        internal static object[] Execute(params Func<SQLiteConnection, object>[] callbackFuncs) {
            SQLiteConnection con = null;
            var affected = new object[callbackFuncs.Length];

//#if DEBUG
//            var now = DateTime.Now;
//#endif
            Monitor.Enter(lock_ThumbDb);
//#if DEBUG
//            Console.WriteLine("SQLiteHelper.Execute() waited " + (DateTime.Now - now).TotalMilliseconds + "ms");
//#endif

            try {
                con = new SQLiteConnection($"Data Source={DbFileFullPath};Version=3;");
                con.Open();
                for (int i = 0; i < callbackFuncs.Length; i++) {
                    affected[i] = callbackFuncs[i].Invoke(con);
                }
            }
            finally {
                if (con != null) {
                    con.Close();
                    con.Dispose();
                }
                Monitor.Exit(lock_ThumbDb);
            }
            return affected;
        }


        internal static int AddToThumbDB(ImageSource source, string path, System.Drawing.Size decodeSize) {
            if (!(source is BitmapSource bs)) throw new NotSupportedException();

            object[] affected = null;
            byte[] png;
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bs));
            using (var ms = new MemoryStream()) {
                enc.Save(ms);
                png = ms.ToArray();
            }
            if (png.Length == 0) return 0;

            affected = Execute(con => {
                using (var cmd = new SQLiteCommand(con)) {
                    //remove existing
                    cmd.CommandText = $@"delete from {Table_ThumbsData.Name} where {Table_ThumbsData.Col_VirtualPath} = @path";
                    cmd.Parameters.Add(new SQLiteParameter("@path", System.Data.DbType.String) { Value = path });
                    cmd.ExecuteNonQuery();
                    //insert new
                    cmd.CommandText = $@"insert into {Table_ThumbsData.Name}
({Table_ThumbsData.Col_VirtualPath}, {Table_ThumbsData.Col_DecodeWidth}, {Table_ThumbsData.Col_DecodeHeight}, {Table_ThumbsData.Col_ThumbData}) values
(@path, {decodeSize.Width}, {decodeSize.Height}, @png)";
                    cmd.Parameters.Add(new SQLiteParameter("@png", System.Data.DbType.Binary) { Value = png });
                    return cmd.ExecuteNonQuery();
                }
            });

            return (int)affected[0];
        }

        /// <summary>
        /// Returns null if thumb either does not exist in DB or has different size.
        /// </summary>
        internal static BitmapSource GetFromThumbDB(string path, System.Drawing.Size decodeSize) {
            var png = Execute(con => {
                byte[] pngByte = null;
                using (var cmd = new SQLiteCommand(con)) {
                    cmd.CommandText =
$@"select {Table_ThumbsData.Col_ThumbData} from {Table_ThumbsData.Name} where
{Table_ThumbsData.Col_VirtualPath} = @path and
{Table_ThumbsData.Col_DecodeWidth} = {decodeSize.Width} and
{Table_ThumbsData.Col_DecodeHeight} = {decodeSize.Height}";
                    cmd.Parameters.Add(new SQLiteParameter("@path", System.Data.DbType.String) { Value = path });
                    using (var reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            pngByte = (byte[])reader["thumbData"];
                            break;
                        }
                    }
                }
                return pngByte;
            });

            if (png.Length == 0 || png[0] == null || ((byte[])png[0]).Length == 0) return null;
            using (var ms = new MemoryStream((byte[])png[0])) {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }
    }
}
