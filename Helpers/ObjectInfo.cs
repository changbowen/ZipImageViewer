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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
                if (Flags.HasFlag(FileFlags.Archive))
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VirtualPath)));
                if (Flags.HasFlag(FileFlags.Image) && Flags.HasFlag(FileFlags.Archive))
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Flags)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VirtualPath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContainerPath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
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

        /// <summary>
        /// Return the immediate container path. If self is a container, same as FileSystemPath.
        /// </summary>
        public string ContainerPath {
            get {
                if (Flags.HasFlag(FileFlags.Directory) ||
                    Flags.HasFlag(FileFlags.Archive))
                    return FileSystemPath;
                else
                    return Path.GetDirectoryName(FileSystemPath);
            }
        }

        private string[] sourcePaths;
        /// <summary>
        /// Contains the child items. Null indicates the children are not retrived yet.
        /// </summary>
        public string[] SourcePaths {
            get => sourcePaths;
            set {
                if (sourcePaths == value) return;
                sourcePaths = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourcePaths)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
            }
        }

        private ImageSource imageSource;
        /// <summary>
        /// Only used for displaying images in archives because it is not practical to start more than one Task to extract an archive.
        /// Thus loading images in archive will be a single thread.
        /// </summary>
        public ImageSource ImageSource {
            get => imageSource;
            set {
                if (imageSource == value) return;
                imageSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageSource)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Comments)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
            }
        }

        //public bool? HasImage {
        //    get {
        //        if (Flags == FileFlags.Unknown || Flags.HasFlag(FileFlags.Error)) return false;
        //        if (SourcePaths != null) {
        //            if (SourcePaths.Length == 0) return false;
        //            else return true;
        //        }
        //        if (ImageSource != null) return true;
        //        return null;
        //    }
        //    //set {
        //    //    if (notAnImage == value) return;
        //    //    notAnImage = value;
        //    //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NotAnImage)));
        //    //}
        //}


        public ObjectInfo(string fsPath, FileFlags flag = FileFlags.Unknown) {
            FileSystemPath = fsPath;
            flags = flag;
        }
    }


}
