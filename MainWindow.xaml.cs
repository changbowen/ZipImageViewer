using System;
using System.IO;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ZipImageViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWin_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() => AddImages(new [] { @"\\192.168.1.250\hdd_private\Pictures\苏夏妞妞" }));

        }

        private void MainWin_Drop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            Task.Run(() => AddImages(paths));
        }

        private void AddImages(string[] paths)
        {
            foreach (var path in paths)
            {
                // check if path is a file or directory
                if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    //directory
                    foreach (var file in Directory.GetFiles(path, "*.jpg", SearchOption.TopDirectoryOnly))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var tn = new Thumbnail(file);
                            WP1.Children.Add(tn);
                        }, System.Windows.Threading.DispatcherPriority.Background);
                        
                    }
                }
                else
                {
                    //file

                }
            }
        }


    }
}
