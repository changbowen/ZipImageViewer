using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using IniParser;
using SevenZip;

namespace ZipImageViewer
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow() {
            InitializeComponent();
        }

        private void SettingsWin_Loaded(object sender, RoutedEventArgs e) {
            Setting.LoadConfigFromFile();
            TB_7zDllPath.Text =     Setting.SevenZipDllPath;
            TB_ThumbWidth.Text =    Setting.ThumbnailSize.Width.ToString();
            TB_ThumbHeight.Text =   Setting.ThumbnailSize.Height.ToString();
            TB_SavedPasswords.Text = Setting.SerializePasswords();
        }

        private void Btn_OK_Click(object sender, RoutedEventArgs e) {
            try {
                Setting.SevenZipDllPath = TB_7zDllPath.Text;
                Setting.ThumbnailSize = new System.Drawing.Size(int.Parse(TB_ThumbWidth.Text), int.Parse(TB_ThumbHeight.Text));
                Setting.SaveConfigToFile(serPwds: TB_SavedPasswords.Text);
                Close();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private void Btn_Cancel_Click(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
