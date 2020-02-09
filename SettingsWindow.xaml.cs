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
        public SettingsWindow(Window owner) {
            Owner = owner;
            InitializeComponent();
        }

        private void SettingsWin_Loaded(object sender, RoutedEventArgs e) {
            Setting.LoadConfigFromFile();
            //TB_7zDllPath.Text =                 Setting.SevenZipDllPath;
            TB_ThumbWidth.Text =                Setting.ThumbnailSize.Width.ToString();
            TB_ThumbHeight.Text =               Setting.ThumbnailSize.Height.ToString();
            SL_ThumbDbSize.Value =              Setting.ThumbDbSize / 1024d;
            CB_ViewerTransition.ItemsSource =   Enum.GetValues(typeof(Setting.Transition));
            CB_ViewerTransition.SelectedItem =  Setting.ViewerTransition;
            CB_AnimSpeed.ItemsSource =          Enum.GetValues(typeof(Setting.TransitionSpeed));
            CB_AnimSpeed.SelectedItem =         Setting.ViewerTransitionSpeed;

            //TB_SavedPasswords.Text =            Setting.SerializePasswords();
        }

        private void Btn_OK_Click(object sender, RoutedEventArgs e) {
            try {
                //Setting.SevenZipDllPath = TB_7zDllPath.Text;
                Setting.ThumbnailSize = new System.Drawing.Size(int.Parse(TB_ThumbWidth.Text), int.Parse(TB_ThumbHeight.Text));
                Setting.ThumbDbSize = (int)(SL_ThumbDbSize.Value * 1024);
                Setting.ViewerTransition = (Setting.Transition)CB_ViewerTransition.SelectedItem;
                Setting.ViewerTransitionSpeed = (Setting.TransitionSpeed)CB_AnimSpeed.SelectedItem;
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
    }
}
