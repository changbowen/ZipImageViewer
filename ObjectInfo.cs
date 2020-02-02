using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace ZipImageViewer
{
    //public class ImageInfo
    //{
    //    public ImageSource ImageSource { get; set; }

    //    public ImageInfo(ImageSource source) {
    //        ImageSource = source;
    //    }

    //    public ImageInfo() { }
    //}

    public class ObjectInfo
    {
        /// <summary>
        /// For archives, relative path of the file inside the archive. Otherwise name of the file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// For archives, full path to the archive file.
        /// For directories, full path to the directory.
        /// Otherwise full path to the image file.
        /// </summary>
        public string FileSystemPath { get; }

        /// <summary>
        /// Indicates the flags of the file. Affects click operations etc.
        /// </summary>
        public FileFlags Flags { get; set; }

        /// <summary>
        /// A virtual path used to avoid duplicated paths in a collection for zipped files.
        /// For non-zip files, same as FileSystemPath.
        /// </summary>
        public string VirtualPath {
            get {
                if (Flags.HasFlag(FileFlags.Archive))
                    return FileSystemPath + @"\" + FileName;
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

        /// <summary>
        /// ImageSources is used in both thumbnail display and ViewWindow.
        /// Whether it is a thumbnail depends on the decode width & height used when loading the image.
        /// </summary>
        public List<ImageSource> ImageSources { get; set; } = new List<ImageSource>();

        public string DebugInfo {
            get {
                return $"{nameof(FileName)}: {FileName}\r\n" +
                    $"{nameof(FileSystemPath)}: {FileSystemPath}\r\n" +
                    $"{nameof(Flags)}: {Flags.ToString()}\r\n" +
                    $"{nameof(ImageSources)}: {string.Join("\r\n", ImageSources.Select(i => i.Width + " x " + i.Height))}\r\n" +
                    $"{nameof(VirtualPath)}: {VirtualPath}\r\n" +
                    $"{nameof(DisplayName)}: {DisplayName}";
            }
        }


        public ObjectInfo(string fileSystemPath, FileFlags flags) {
            FileSystemPath = fileSystemPath;
            Flags = flags;
        }

    }
}
