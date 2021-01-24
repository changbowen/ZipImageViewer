using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZipImageViewer
{
    public static class TableHelper
    {
        public enum Table
        { Thumbs, MappedPasswords, FallbackPasswords }

        public enum Column
        {
            BasePath, SubPath, DecodeWidth, DecodeHeight, ThumbData,//Thumbs table
            Path, Password,//MappedPasswords and FallbackPasswords table
        }

        public class TableInfo
        {
            public readonly string Name;
            public string FileName => Name + @".db";
            public string FullPath => Path.Combine(Setting.DatabaseDir, FileName);
            public readonly object Lock = new object();
            public TableInfo(Table table) {
                Name = table.ToString();
            }
        }

        /// <summary>
        /// When primKeyData is not found in the first primary key column, add to the table with new values.
        /// Otherwise update existing one.
        /// </summary>
        public static void UpdateDataTable(this DataTable dt, object primKeyData, string columnName, object newData) {
            var row = dt.Rows.Find(primKeyData);
            if (row == null) {
                var newRow = dt.NewRow();
                newRow[dt.PrimaryKey[0]] = primKeyData;
                newRow[columnName] = newData;
                dt.Rows.Add(newRow);
            }
            else {
                row[columnName] = newData;
            }
        }

        public static void RowChangeHandler(object sender, DataRowChangeEventArgs e) {
            if (!(DataRowAction.Add | DataRowAction.Change | DataRowAction.ChangeCurrentAndOriginal | DataRowAction.ChangeOriginal).HasFlag(e.Action)) return;

            var rawPwd = (string)e.Row[nameof(Column.Password)];
            if (!EncryptionHelper.IsEncrypted(rawPwd))
                e.Row[nameof(Column.Password)] = EncryptionHelper.Encrypt(rawPwd);
        }
    }
}
