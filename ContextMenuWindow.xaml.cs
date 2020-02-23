using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ZipImageViewer
{
    public partial class ContextMenuWindow : RoundedWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        public ContextMenuWindow() {
            InitializeComponent();
        }

        private void ObjectInfo_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            var oi = (ObjectInfo)sender;
            //handle menu enable / disable here
        }

        private ObjectInfo objectInfo;
        public ObjectInfo ObjectInfo {
            get => objectInfo;
            set {
                if (objectInfo == value) return;
                objectInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ObjectInfo)));
            }
        }


        private void Menu_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            if (ObjectInfo == null) return;
            if (!(Owner is MainWindow mainWin)) return;

            var border = (Border)sender;
            switch (border.Name) {
                case nameof(B_OpenInExplorer):
                    Helpers.Run("explorer", $"/select, \"{ObjectInfo.FileSystemPath}\"");
                    break;
                case nameof(B_OpenInNewWindow):
                    if (ObjectInfo.Flags.HasFlag(FileFlags.Image)) {
                        mainWin.LoadPath(ObjectInfo);
                    }
                    else if (ObjectInfo.Flags.HasFlag(FileFlags.Directory) ||
                        ObjectInfo.Flags.HasFlag(FileFlags.Archive)) {
                        var win = new MainWindow {
                            InitialPath = ObjectInfo.FileSystemPath
                        };
                        win.Show();
                    }
                    break;
                case nameof(B_Slideshow):
                    var sldWin = new SlideshowWindow(ObjectInfo);
                    sldWin.Show();
                    break;
            }

            Close();
            ObjectInfo = null;
        }

    }
}
