using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using SizeInt = System.Drawing.Size;

namespace ZipImageViewer
{
    [System.Diagnostics.DebuggerDisplay("{Flags}: {VirtualPath}")]
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
                if (Flags.HasFlag(FileFlags.Archive))
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VirtualPath)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VirtualPath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContainerPath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsContainer)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugInfo)));
            }
        }

        /// <summary>
        /// A virtual path used to avoid duplicated paths in a collection for zipped files.
        /// For non-zip files, same as FileSystemPath.
        /// </summary>
        public string VirtualPath {
            get {
                if ((Flags.HasFlag(FileFlags.Archive) && Flags.HasFlag(FileFlags.Image)) ||
                    (Flags.HasFlag(FileFlags.Directory) && Flags.HasFlag(FileFlags.Image)))
                    return Path.Combine(FileSystemPath, FileName);
                return FileSystemPath;
            }
        }

        public string DisplayName {
            get => FileName;
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

        public bool IsContainer {
            get {
                if (Flags.HasFlag(FileFlags.Directory) ||
                    Flags.HasFlag(FileFlags.Archive))
                    return true;
                else
                    return false;
            }
        }

        private string[] sourcePaths;
        /// <summary>
        /// Contains the child items. Null indicates the children are not retrived yet.
        /// For containers, the paths relative to the container's path.
        /// For images, this should be a single-element array with the image's FileName in it.
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
        /// Be careful when setting this property. Clean up when necessary to avoid memory leaks.
        /// </summary>
        public ImageSource ImageSource {
            get => imageSource;
            set {
                if (imageSource == value) return;
                imageSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageSource)));
            }
        }

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
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

        /// <summary>
        /// Setting <paramref name="fName"/> when you can otherwise it will be set based on FileSystemPath if it's not an archive.
        /// </summary>
        public ObjectInfo(string fsPath, FileFlags flag, string fName = null) {
            FileSystemPath = fsPath;
            flags = flag;
            fileName = fName;
            if (fileName == null && !flags.HasFlag(FileFlags.Archive))
                fileName = Path.GetFileName(FileSystemPath);
        }
    }

    public class ImageInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private long fileSize;
        public long FileSize {
            get => fileSize;
            set {
                if (fileSize == value) return;
                fileSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
            }
        }

        private SizeInt dimensions;
        public SizeInt Dimensions {
            get => dimensions;
            set {
                if (dimensions == value) return;
                dimensions = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Dimensions)));
            }
        }

        private DateTime created;
        public DateTime Created {
            get => created;
            set {
                if (created == value) return;
                created = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Created)));
            }
        }

        private DateTime modified;
        public DateTime Modified {
            get => modified;
            set {
                if (modified == value) return;
                modified = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Modified)));
            }
        }

        private string meta_DateTaken;
        public string Meta_DateTaken {
            get => meta_DateTaken;
            set {
                if (meta_DateTaken == value) return;
                meta_DateTaken = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Meta_DateTaken)));
            }
        }

        private string meta_Camera;
        public string Meta_Camera {
            get => meta_Camera;
            set {
                if (meta_Camera == value) return;
                meta_Camera = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Meta_Camera)));
            }
        }

        private string meta_applicationName;
        public string Meta_ApplicationName {
            get => meta_applicationName;
            set {
                if (meta_applicationName == value) return;
                meta_applicationName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Meta_ApplicationName)));
            }
        }


        public ImageInfo() { }

    }
}
