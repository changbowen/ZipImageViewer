using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ZipImageViewer
{
    public class SlideshowHelper
    {
        public class SlideAnimConfig
        {
            public int ImageDuration = 7;
            public double XPanDistanceR = 1d;
            public double YPanDistanceR = 1d;
        }

        private static readonly Random ran = new Random();

        /// <summary>
        /// Returns the time until the next instance should be started.
        /// </summary>
        public static TimeSpan Anim_KBE(DpiImage tgtImg, Size frameSize, SlideAnimConfig animCfg) {
            var imageDur = new Duration(new TimeSpan(0, 0, animCfg.ImageDuration));
            var zoomIn = ran.Next(2) == 0;
            var panLeftTop = ran.Next(2) == 0;

            //zoon animation
            var animZoom = zoomIn ? new DoubleAnimation(1d, 1.2d, imageDur) : new DoubleAnimation(1.2d, 1d, imageDur);

            //pan animation
            DoubleAnimation animPan;
            DependencyProperty transEdge;
            double delta;
            double startPoint;
            var rCanvas = frameSize.Width / frameSize.Height;
            var rImage = tgtImg.RealSize.Width / tgtImg.RealSize.Height;
            if (rImage > rCanvas) {
                //width is the longer edge comparing to the size of the canvas
                tgtImg.Height = frameSize.Height;
                tgtImg.Width = tgtImg.Height * rImage;
                transEdge = TranslateTransform.XProperty;
                tgtImg.RenderTransformOrigin = panLeftTop ? new Point(0d, 0.5d) : new Point(1d, 0.5d);

                delta = tgtImg.Width - frameSize.Width;
                startPoint = delta * animCfg.XPanDistanceR;
                animPan = panLeftTop ?
                    new DoubleAnimation(startPoint - delta, zoomIn ? -delta - tgtImg.Width * 0.2d : -delta, imageDur) :
                    new DoubleAnimation(-startPoint, zoomIn ? tgtImg.Width * 0.2d : 0d, imageDur);
            }
            else {
                //height is the longer edge comparing to the size of the canvas
                tgtImg.Width = frameSize.Width;
                tgtImg.Height = tgtImg.Width / rImage;
                transEdge = TranslateTransform.YProperty;
                tgtImg.RenderTransformOrigin = panLeftTop ? new Point(0.5d, 0d) : new Point(0.5d, 1d);

                delta = tgtImg.Height - frameSize.Height;
                startPoint = delta * animCfg.YPanDistanceR;
                animPan = panLeftTop ?
                    new DoubleAnimation(startPoint - delta, zoomIn ? -delta - tgtImg.Height * 0.2d : -delta, imageDur) :
                    new DoubleAnimation(-startPoint, zoomIn ? tgtImg.Height * 0.2d : 0d, imageDur);
            }

            //fade animation
            var animFade = new DoubleAnimationUsingKeyFrames();
            animFade.KeyFrames.Add(new LinearDoubleKeyFrame(0d, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animFade.KeyFrames.Add(new LinearDoubleKeyFrame(1d, KeyTime.FromTimeSpan(new TimeSpan(0, 0, 1))));
            animFade.KeyFrames.Add(new LinearDoubleKeyFrame(1d, KeyTime.FromTimeSpan(new TimeSpan(0, 0, animCfg.ImageDuration - 1))));
            animFade.KeyFrames.Add(new LinearDoubleKeyFrame(0d, KeyTime.FromTimeSpan(new TimeSpan(0, 0, animCfg.ImageDuration))));

            var tg = (TransformGroup)tgtImg.RenderTransform;
            tg.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, animZoom);
            tg.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, animZoom);
            tg.Children[1].BeginAnimation(transEdge, animPan);
            tgtImg.BeginAnimation(UIElement.OpacityProperty, animFade);

            return new TimeSpan(0, 0, animCfg.ImageDuration - 2);
        }
    }
}
