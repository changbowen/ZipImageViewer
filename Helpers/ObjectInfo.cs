using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace ZipImageViewer
{
    public class ObjectInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string fileName;
        /// <summary>
        /// For archives, relative path of the file inside the archive. Otherwise name of the file.
        /// </summary>
        public string FileName {
            get => fileName;
            set {
                if (fileName == value) return;
                fileName = value;
                if (PropertyChanged == null) return;
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileName)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
                if (Flags.HasFlag(FileFlags.Archive))
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(VirtualPath)));
                if (Flags.HasFlag(FileFlags.Image) && Flags.HasFlag(FileFlags.Archive))
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        /// <summary>
        /// For archives, full path to the archive file.
        /// For directories, full path to the directory.
        /// Otherwise full path to the image file.
        /// </summary>
        public string FileSystemPath { get; }

        private FileFlags flags;
        /// <summary>
        /// Indicates the flags of the file. Affects click operations etc.
        /// </summary>
        public FileFlags Flags {
            get => flags;
            set {
                if (flags == value) return;
                flags = value;
                if (PropertyChanged == null) return;
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Flags)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(VirtualPath)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Parent)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
            }
        }

        /// <summary>
        /// A virtual path used to avoid duplicated paths in a collection for zipped files.
        /// For non-zip files, same as FileSystemPath.
        /// </summary>
        public string VirtualPath {
            get {
                if (Flags.HasFlag(FileFlags.Archive) && Flags.HasFlag(FileFlags.Image))
                    return Path.Combine(FileSystemPath, FileName);
                return FileSystemPath;
            }
        }

        /// <summary>
        /// For directory and archive, the parent folder name of FilePath. Otherwise FileName.
        /// </summary>
        public string DisplayName {
            get {
                if (Flags.HasFlag(FileFlags.Directory))
                    return Path.GetFileName(FileSystemPath);
                if (Flags.HasFlag(FileFlags.Image) && Flags.HasFlag(FileFlags.Archive))
                    return FileName;
                return FileName;
            }
        }

        public string Parent {
            get {
                if (Flags.HasFlag(FileFlags.Directory) || Flags.HasFlag(FileFlags.Archive))
                    return Path.GetDirectoryName(FileSystemPath);
                else
                    return Path.GetDirectoryName(Path.GetDirectoryName(FileSystemPath));
            }
        }

        private string[] sourcePaths;
        /// <summary>
        /// ImageSources is used in both thumbnail display and ViewWindow.
        /// Whether it is a thumbnail depends on the decode width & height used when loading the image.
        /// </summary>
        public string[] SourcePaths {
            get => sourcePaths;
            set {
                if (sourcePaths == value) return;
                sourcePaths = value;
                if (PropertyChanged == null) return;
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(SourcePaths)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
            }
        }

        public string DebugInfo {
            get {
                return $"{nameof(FileName)}: {FileName}\r\n" +
                    $"{nameof(FileSystemPath)}: {FileSystemPath}\r\n" +
                    $"{nameof(Flags)}: {Flags.ToString()}\r\n" +
                    $"{nameof(SourcePaths)}: {SourcePaths?.Length}\r\n" +
                    $"{nameof(VirtualPath)}: {VirtualPath}\r\n" +
                    $"{nameof(DisplayName)}: {DisplayName}\r\n" +
                    $"{nameof(Comments)}:\r\n{Comments}";
            }
        }

        private string comments;
        public string Comments {
            get => comments;
            set {
                if (comments == value) return;
                comments = value;
                if (PropertyChanged == null) return;
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Comments)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
            }
        }


        public ObjectInfo(string fsPath, FileFlags flag = FileFlags.Unknown, string[] paths = null) {
            FileSystemPath = fsPath;
            flags = flag;
            sourcePaths = paths;
        }
    }


}
