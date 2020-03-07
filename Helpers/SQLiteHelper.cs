using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.Helpers;
using static ZipImageViewer.TableHelper;
using System.Threading.Tasks;

namespace ZipImageViewer
{
    internal static class SQLiteHelper
    {
        internal static readonly Dictionary<Table, TableInfo> Tables =
            new Dictionary<Table, TableInfo>() {
                { Table.Thumbs,          new TableInfo(Table.Thumbs) },
                { Table.MappedPasswords, new TableInfo(Table.MappedPasswords) },
            };

        /// <summary>
        /// Returns an array of objects containing the return value from each Func.
        /// Errors in callbacks will be ignored and the next callback will be executed;
        /// </summary>
        internal static object[] Execute(Table t, params Func<TableInfo, SQLiteConnection, object>[] callbackFuncs) {
            SQLiteConnection con = null;
            var affected = new object[callbackFuncs.Length];
            var table = Tables[t];

            Monitor.Enter(table.Lock);
            try {
                con = new SQLiteConnection($"Data Source={table.FullPath};Version=3;");
                con.Open();
                for (int i = 0; i < callbackFuncs.Length; i++) {
                    try { affected[i] = callbackFuncs[i].Invoke(table, con); }
                    catch { }
                }
            }
            catch (Exception ex) {
                MessageBox.Show(GetRes("msg_ErrorOpenDbFile", table.FullPath) + $"\r\n{ex.Message}", null, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally {
                if (con != null) {
                    con.Close();
                    con.Dispose();
                }
                Monitor.Exit(table.Lock);
            }
            return affected;
        }

        internal static void CheckThumbsDB() {
            var goodColumns = 0;
            Execute(Table.Thumbs, (table, con) => {
                using (var cmd = new SQLiteCommand(con)) {
                    cmd.CommandText = $@"pragma table_info({table.Name})";
                    using (var r = cmd.ExecuteReader()) {
                        while (r.Read()) {
                            switch (r["name"]) {
                                case nameof(Column.BasePath):
                                    if (r["type"].ToString() == "TEXT" &&
                                        r["notnull"].ToString() == "1") goodColumns += 1;
                                    break;
                                case nameof(Column.SubPath):
                                    if (r["type"].ToString() == "TEXT" &&
                                        r["notnull"].ToString() == "1") goodColumns += 1;
                                    break;
                                case nameof(Column.DecodeWidth):
                                case nameof(Column.DecodeHeight):
                                    if ((string)r["type"] == "INTEGER") goodColumns += 1;
                                    break;
                                case nameof(Column.ThumbData):
                                    if ((string)r["type"] == "BLOB") goodColumns += 1;
                                    break;
                            }
                        }
                        return 0;
                    }
                }
            });
            if (goodColumns == 5) return;

            //recreate thumbs table
            if (File.Exists(Tables[Table.Thumbs].FullPath))
                File.Delete(Tables[Table.Thumbs].FullPath);
            else
                Directory.CreateDirectory(Setting.DatabaseDir);

            Execute(Table.Thumbs, (table, con) => {
                using (var cmd = new SQLiteCommand(con)) {
                    cmd.CommandText =
$@"create table if not exists [{table.Name}] (
[{Column.BasePath}] TEXT NOT NULL,
[{Column.SubPath}] TEXT NOT NULL,
[{Column.DecodeWidth}] INTEGER,
[{Column.DecodeHeight}] INTEGER,
[{Column.ThumbData}] BLOB)";
                    return cmd.ExecuteNonQuery();
                }
            });
        }

        internal static int AddToThumbDB(ImageSource source, string basePath, string subPath, SizeInt decodeSize) {
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

            affected = Execute(Table.Thumbs, (table, con) => {
                using (var cmd = new SQLiteCommand(con)) {
                    //remove existing
                    cmd.CommandText = 
$@"delete from {table.Name} where
{Column.BasePath} = @basePath and
{Column.SubPath} = @subPath";
                    cmd.Parameters.Add(new SQLiteParameter("@basePath", DbType.String) { Value = basePath });
                    cmd.Parameters.Add(new SQLiteParameter("@subPath", DbType.String) { Value = subPath });
                    cmd.ExecuteNonQuery();
                    //insert new
                    cmd.CommandText = 
$@"insert into {table.Name}
({Column.BasePath}, {Column.SubPath}, {Column.DecodeWidth}, {Column.DecodeHeight}, {Column.ThumbData}) values
(@basePath, @subPath, {decodeSize.Width}, {decodeSize.Height}, @png)";
                    cmd.Parameters.Add(new SQLiteParameter("@png", DbType.Binary) { Value = png });
                    return cmd.ExecuteNonQuery();
                }
            });

            return (int)affected[0];
        }

        /// <summary>
        /// Async version of <see cref="GetFromThumbDB(string, string, SizeInt)"/>
        /// </summary>
        internal static Task<Tuple<BitmapSource, string>> GetFromThumbDBAsync(string basePath, SizeInt decodeSize, string subPath = null) {
            return Task.Run(() => GetFromThumbDB(basePath, decodeSize, subPath));
        }

        /// <summary>
        /// Returns null if thumb either does not exist in DB or has different size.
        /// If <paramref name="subPath"/> is null, the first match by <paramref name="basePath"/> will be returned.
        /// </summary>
        internal static Tuple<BitmapSource, string> GetFromThumbDB(string basePath, SizeInt decodeSize, string subPath = null) {
            var png = Execute(Table.Thumbs, (table, con) => {
                byte[] pngByte = null;
                using (var cmd = new SQLiteCommand(con)) {
                    cmd.CommandText =
$@"select * from {table.Name} where
{Column.BasePath} = @basePath
{(subPath == null ? "" : $@"and {Column.SubPath} = @subPath")} and
{Column.DecodeWidth} = {decodeSize.Width} and
{Column.DecodeHeight} = {decodeSize.Height} limit 1";
                    cmd.Parameters.Add(new SQLiteParameter("@basePath", DbType.String) { Value = basePath });
                    if (subPath != null)
                        cmd.Parameters.Add(new SQLiteParameter("@subPath", DbType.String) { Value = subPath });
                    using (var reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            pngByte = (byte[])reader[nameof(Column.ThumbData)];
                            if (subPath == null)
                                subPath = (string)reader[nameof(Column.SubPath)];
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
                return new Tuple<BitmapSource, string>(bi, subPath);
            }
        }

        internal static bool ThumbExistInDB(string basePath, string subPath, SizeInt decodeSize) {
            return (bool)Execute(Table.Thumbs, (table, con) => {
                using (var cmd = new SQLiteCommand(con)) {
                    cmd.CommandText =
$@"select count({Column.ThumbData}) from {table.Name} where
{Column.BasePath} = @basePath
{(subPath == null ? "" : $@"and {Column.SubPath} = @subPath")} and
{Column.DecodeWidth} = {decodeSize.Width} and
{Column.DecodeHeight} = {decodeSize.Height}";
                    cmd.Parameters.Add(new SQLiteParameter("@basePath", DbType.String) { Value = basePath });
                    if (subPath != null)
                        cmd.Parameters.Add(new SQLiteParameter("@subPath", DbType.String) { Value = subPath });
                    using (var r = cmd.ExecuteReader()) {
                        r.Read();
                        return (long)r[0] > 0;
                    }
                }
            })[0];
        }
    }
}
