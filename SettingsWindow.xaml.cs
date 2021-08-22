using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static ZipImageViewer.Helpers;
using static ZipImageViewer.TableHelper;
using static ZipImageViewer.SQLiteHelper;
using System.Data;

namespace ZipImageViewer
{
    public partial class SettingsWindow : BorderlessWindow
    {
        public SettingsWindow(Window owner) {
            Owner = owner;
            InitializeComponent();
        }

        public string CurrentThumbDbSize {
            get { return (string)GetValue(CurrentThumbDbSizeProperty); }
            set { SetValue(CurrentThumbDbSizeProperty, value); }
        }
        public static readonly DependencyProperty CurrentThumbDbSizeProperty =
            DependencyProperty.Register("CurrentThumbDbSize", typeof(string), typeof(SettingsWindow), new PropertyMetadata("N/A"));


        private void SettingsWin_Loaded(object sender, RoutedEventArgs e) {
            CurrentThumbDbSize = BytesToString(new FileInfo(Tables[Table.Thumbs].FullPath).Length);
        }

        private void SettingsWin_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            try {
                Setting.FallbackPasswords.AcceptChanges();
                Setting.MappedPasswords.AcceptChanges();
                Setting.SaveConfigs();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private void Btn_ChgMstPwd_Click(object sender, RoutedEventArgs e) {
            bool showIncorrect = false, showMismatch = false;
            for (int i = 0; i < 10; i++) {
                var (answer, curPwd, newPwd, cfmPwd) = InputWindow.PromptForPasswordChange(true, showIncorrect, showMismatch);
                if (!answer) return;
                showIncorrect = false;
                showMismatch = false;
                if (curPwd != Setting.MasterPassword) showIncorrect = true;
                else if (newPwd != cfmPwd) showMismatch = true;
                else {
                    Setting.ChangeMasterPassword(newPwd, curPwd);
                    break;
                }
            }
        }

        private void SettingsWin_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if (e.Key != System.Windows.Input.Key.Escape || !(e.Source is ScrollViewer)) return;
            Close();
        }

        private async void Btn_Move_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrWhiteSpace(TB_DatabaseDir.Text)) return;
            var sourceDir = Path.GetFullPath(Setting.DatabaseDir).TrimEnd(Path.DirectorySeparatorChar);
            var targetDir = TB_DatabaseDir.Text.Trim();

            //try to move file if dir is not same
            if (sourceDir != Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar)) {
                try {
                    ((Button)sender).IsEnabled = false;

                    Directory.CreateDirectory(targetDir);
                    await Task.Run(() => {
                        foreach (var table in Tables.Values) {
                            if (!File.Exists(table.FullPath)) continue;
                            lock (table.Lock) {
                                var targetPath = Path.Combine(targetDir, table.FileName);
                                if (File.Exists(targetPath)) throw new Exception(GetRes("msg_File_0_Exists", targetPath));
                                File.Move(table.FullPath, targetPath);
                            }
                        }
                    });

                    MessageBox.Show(GetRes("msg_DbMovedSucc"), GetRes("ttl_OperationComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) {
                    MessageBox.Show(ex.Message, GetRes("ttl_OperationFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                finally {
                    ((Button)sender).IsEnabled = true;
                }
            }
            else {
                MessageBox.Show(GetRes("msg_DbPathUpdated"), GetRes("ttl_OperationComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Setting.DatabaseDir = targetDir;
        }

        private void Btn_Clean_Click(object sender, RoutedEventArgs e) {
            //clean database
            Execute(Table.Thumbs, (table, con) => {
                using (var cmd = new SQLiteCommand(con)) {
                    cmd.CommandText = $@"delete from {table.Name}";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = @"vacuum";
                    cmd.ExecuteNonQuery();
                }
                return 0;
            });

            CurrentThumbDbSize = BytesToString(new FileInfo(Tables[Table.Thumbs].FullPath).Length);
        }

        private void Btn_Reload_Click(object sender, RoutedEventArgs e) {
            if (!(Owner is MainWindow win)) return;
            Task.Run(() => win.LoadPath(win.CurrentPath));
        }

        private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e) {
            var dg = (DataGrid)sender;
            if (e.EditAction != DataGridEditAction.Commit) return;

            //due to the UpdateSourceTrigger is LostFocus for Text, without this e.Row.Item wont have the new value
            e.Row.BindingGroup.UpdateSources();
            switch (e.Row.Item) {
                case ObservablePair<string, string> op:
                    if (string.IsNullOrWhiteSpace(op.Item1) ||
                        string.IsNullOrWhiteSpace(op.Item2)) {
                        //dg.CancelEdit(); requires implementing IEditableObject on ObservablePair
                        ((Collection<ObservablePair<string, string>>)dg.ItemsSource).Remove(op);
                    }
                    return;
                case Observable<string> o:
                    if (string.IsNullOrWhiteSpace(o.Item))
                        ((Collection<Observable<string>>)dg.ItemsSource).Remove(o);
                    return;

            }
        }

    }

    //public class ObservablesValidationRule : ValidationRule
    //{
    //    public bool ValidateItem1 { get; set; } = true;
    //    public bool ValidateItem2 { get; set; } = true;

    //    public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
    //        var bg = (BindingGroup)value;
    //        switch (bg.Items[0]) {
    //            case ObservablePair<string, string> op:
    //                if ((ValidateItem1 && string.IsNullOrWhiteSpace(op.Item1)) ||
    //                    (ValidateItem2 && string.IsNullOrWhiteSpace(op.Item2)))
    //                    return new ValidationResult(false, "Empty values are not allowed.");
    //                break;
    //            case Observable<string> o:
    //                if (ValidateItem1 && string.IsNullOrWhiteSpace(o.Item))
    //                    return new ValidationResult(false, "Empty values are not allowed.");
    //                break;
    //        }
    //        return ValidationResult.ValidResult;
    //    }
    //}

}
