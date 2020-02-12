using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using IniParser;
using SevenZip;

namespace ZipImageViewer
{
    public partial class SettingsWindow : BorderlessWindow
    {
        public SettingsWindow(Window owner) {
            Owner = owner;
            InitializeComponent();
        }

        private void SettingsWin_Loaded(object sender, RoutedEventArgs e) {
            //Setting.LoadConfigFromFile();
            //TB_7zDllPath.Text =                 Setting.SevenZipDllPath;
            //TB_ThumbWidth.Text =                Setting.ThumbnailSize.Item1.ToString();
            //TB_ThumbHeight.Text =               Setting.ThumbnailSize.Item2.ToString();
            //SL_ThumbDbSize.Value =              Setting.ThumbDbSize / 1024d;
            CB_ViewerTransition.ItemsSource =   Enum.GetValues(typeof(Setting.Transition));
            CB_ViewerTransition.SelectedItem =  Setting.ViewerTransition;
            CB_AnimSpeed.ItemsSource =          Enum.GetValues(typeof(Setting.TransitionSpeed));
            CB_AnimSpeed.SelectedItem =         Setting.ViewerTransitionSpeed;

            T_CurrentDbSize.Text = $"Current DB size: {Helpers.BytesToString(new FileInfo(SQLiteHelper.DbFileFullPath).Length)}";
        }

        private void CB_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var cb = (ComboBox)sender;
            switch (cb.Name) {
                case nameof(CB_ViewerTransition):
                    Setting.ViewerTransition = (Setting.Transition)cb.SelectedItem;
                    break;
                case nameof(CB_AnimSpeed):
                    Setting.ViewerTransitionSpeed = (Setting.TransitionSpeed)cb.SelectedItem;
                    break;
            }
        }

        private async void Btn_Move_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrWhiteSpace(TB_ThumbDbDir.Text)) return;

            var targetDir = TB_ThumbDbDir.Text;
            var btn = (Button)sender;
            btn.IsEnabled = false;
            try {
                await Task.Run(() => File.Move(SQLiteHelper.DbFileFullPath, Path.Combine(targetDir, SQLiteHelper.dbFileName)));

                Setting.ThumbDbDir = targetDir;
                MessageBox.Show("Thumbnail database file moved successfully.", "Move Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                btn.IsEnabled = true;
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Move Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Btn_Reload_Click(object sender, RoutedEventArgs e) {
            if (!(Owner is MainWindow win)) return;
            Task.Run(() => win.LoadPath(win.CurrentPath));
        }

        private void Btn_OK_Click(object sender, RoutedEventArgs e) {
            try {
                //Setting.SevenZipDllPath = TB_7zDllPath.Text;
                //Setting.ThumbnailSize = new ObservablePair<int, int>(int.Parse(TB_ThumbWidth.Text), int.Parse(TB_ThumbHeight.Text));
                //Setting.ThumbDbSize = (int)(SL_ThumbDbSize.Value * 1024);
                //Setting.ViewerTransition = (Setting.Transition)CB_ViewerTransition.SelectedItem;
                //Setting.ViewerTransitionSpeed = (Setting.TransitionSpeed)CB_AnimSpeed.SelectedItem;
                Setting.SaveConfigToFile();
                Close();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private void Btn_Cancel_Click(object sender, RoutedEventArgs e) {
            Close();
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
                    break;
                case Observable<string> o:
                    if (string.IsNullOrWhiteSpace(o.Item)) {
                        ((Collection<Observable<string>>)dg.ItemsSource).Remove(o);
                    }
                    break;
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
