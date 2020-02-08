# ZipImageViewer
Minimalistic image viewer that introduce no "DPI blurriness" and views contents of password-protected archives on the fly.

Need .Net Framework 4.7.2 (included in Windows 10 April 2018 Update)

## Features
- Opens all archives supported by 7z.
- Saves password of each archive for later.
- Tries a configured list of fallback passwords on any new encrypted archives.
- Support EXIF orientation metadata.
- DpiImage control
  - 1:1 rendering of images by overriding WPF's device-independent auto-scaling on Image control.
- No blurriness caused by incorrect position (X / Y translation).
- PerMonitor DPI awareness
- Thumbnail cache for faster loading

