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

## Screenshots
**Be sure to scale the page properly to see the difference.**
**For example if your display is set to 125% scaling, you need to scale the webpage to 80%.**
**And this is actually a chroma test image so you also want to use RGB instead of YCbCr color format on your monitor.**

Microsoft Photos 2019.19071.17920.0 unable to handle proper rendering after zooming and panning.
(Come on Microsoft... Is that the best you can do? And please let me scale to 100% by double clicking!)
<img src="https://github.com/changbowen/Misc/raw/master/ZipImageViewer/chroma_photos.png"/>

ZipImageViewer is always true to the image at 100% scaling.
<img src="https://github.com/changbowen/Misc/raw/master/ZipImageViewer/chroma_zipimageviewer.png"/>

