using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ZipImageViewer
{
    public static class SlideshowHelper
    {
        public enum SlideTransition
        {
            [Description("ttl_" + nameof(KenBurns), true)]
            KenBurns,
            [Description("ttl_" + nameof(Breath), true)]
            Breath,
            [Description("ttl_" + nameof(Emerge), true)]
            Emerge
        }

        public class SlideAnimConfig : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;


            private SlideTransition transition = SlideTransition.KenBurns;
            public SlideTransition Transition {
                get => transition;
                set {
                    if (transition == value) return;
                    transition = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Transition)));
                }
            }

            private TimeSpan transitionDelay = TimeSpan.Zero;
            public TimeSpan TransitionDelay
            {
                get => transitionDelay;
                set {
                    if (transitionDelay == value) return;
                    transitionDelay = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransitionDelay)));
                }
            }

            private TimeSpan imageDuration = new TimeSpan(0, 0, 7);
            public TimeSpan ImageDuration {
                get => imageDuration;
                set {
                    if (imageDuration == value) return;
                    if (value.TotalSeconds < MinImageSeconds)
                        value = TimeSpan.FromSeconds(MinImageSeconds);
                    imageDuration = value;

                    if (PropertyChanged == null) return;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ImageDuration)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Time_Mid)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Time_FadeOutBegin)));
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Time_End)));
                }
            }

            private TimeSpan fadeInDuration = new TimeSpan(0, 0, 2);
            /// <summary>
            /// When setting this property, if total fade time + 1 (seconds) is less than ImageDuration, ImageDuration will be extended.
            /// </summary>
            public TimeSpan FadeInDuration {
                get => fadeInDuration;
                set {
                    if (fadeInDuration == value) return;
                    fadeInDuration = value;

                    if (PropertyChanged != null) {
                        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FadeInDuration)));
                        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Time_FadeInEnd)));
                        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(MinImageSeconds)));
                    }

                    //extend ImageDuration if needed
                    if (ImageDuration.TotalSeconds < MinImageSeconds)
                        ImageDuration = TimeSpan.FromSeconds(MinImageSeconds);
                }
            }

            private TimeSpan fadeOutDuration = new TimeSpan(0, 0, 2);
            /// <summary>
            /// When setting this property, if total fade time + 1 (seconds) is less than ImageDuration, ImageDuration will be extended.
            /// </summary>
            public TimeSpan FadeOutDuration {
                get => fadeOutDuration;
                set {
                    if (fadeOutDuration == value) return;
                    fadeOutDuration = value;

                    if (PropertyChanged != null) {
                        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FadeOutDuration)));
                        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Time_FadeOutBegin)));
                        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(MinImageSeconds)));
                    }

                    //extend ImageDuration if needed
                    if (ImageDuration.TotalSeconds < MinImageSeconds)
                        ImageDuration = TimeSpan.FromSeconds(MinImageSeconds);
                }
            }

            public double MinImageSeconds => FadeInDuration.TotalSeconds + FadeOutDuration.TotalSeconds + 1d;

            private KeyTime time_Zero = KeyTime.FromTimeSpan(TimeSpan.Zero);
            public KeyTime Time_Zero => time_Zero;
            public KeyTime Time_FadeInEnd => KeyTime.FromTimeSpan(FadeInDuration);
            public KeyTime Time_Mid => KeyTime.FromTimeSpan(TimeSpan.FromSeconds(ImageDuration.TotalSeconds / 2));
            public KeyTime Time_FadeOutBegin => KeyTime.FromTimeSpan(ImageDuration.Subtract(FadeOutDuration));
            public KeyTime Time_End => KeyTime.FromTimeSpan(ImageDuration);


            private double xPanDistanceR = 1d;
            public double XPanDistanceR {
                get => xPanDistanceR;
                set {
                    if (xPanDistanceR == value) return;
                    xPanDistanceR = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(XPanDistanceR)));
                }
            }

            private double yPanDistanceR = 1d;
            public double YPanDistanceR {
                get => yPanDistanceR;
                set {
                    if (yPanDistanceR == value) return;
                    yPanDistanceR = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(YPanDistanceR)));
                }
            }

            private bool yPanDownOnly = true;
            public bool YPanDownOnly {
                get => yPanDownOnly;
                set {
                    if (yPanDownOnly == value) return;
                    yPanDownOnly = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(YPanDownOnly)));
                }
            }

            private bool blur = false;
            public bool Blur {
                get => blur;
                set {
                    if (blur == value) return;
                    blur = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Blur)));
                }
            }

            private double resolutionScale = 1d;
            public double ResolutionScale {
                get => resolutionScale;
                set {
                    if (resolutionScale == value) return;
                    resolutionScale = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ResolutionScale)));
                }
            }

            private bool randomOrder = false;
            public bool RandomOrder {
                get => randomOrder;
                set {
                    if (randomOrder == value) return;
                    randomOrder = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RandomOrder)));
                }
            }


            /// <summary>
            /// Status store used by animations
            /// </summary>
            internal bool lastBool1;

            public SlideAnimConfig() { }

            public SlideAnimConfig(SlideTransition transition = SlideTransition.KenBurns, double imgDur = 7d, double fadeInDur = 2d, double fadeOutDur = 2d) {
                Transition = transition;
                ImageDuration = TimeSpan.FromSeconds(imgDur);
                FadeInDuration = TimeSpan.FromSeconds(fadeInDur);
                FadeOutDuration = TimeSpan.FromSeconds(fadeOutDur);
            }

            /// <summary>
            /// Create a copy of the existing config.
            /// </summary>
            public SlideAnimConfig(SlideAnimConfig config) {
                transition = config.transition;
                imageDuration = config.imageDuration;
                fadeInDuration = config.fadeInDuration;
                fadeOutDuration = config.fadeOutDuration;
                xPanDistanceR = config.xPanDistanceR;
                yPanDistanceR = config.yPanDistanceR;
                yPanDownOnly = config.yPanDownOnly;
                blur = config.blur;
                resolutionScale = config.resolutionScale;
            }
        }

        internal static readonly Random ran = new Random();
        private static readonly CubicEase easeOut = new CubicEase() { EasingMode = EasingMode.EaseOut };
        private static readonly CubicEase easeIn = new CubicEase() { EasingMode = EasingMode.EaseIn };
        private static readonly CubicEase easeInOut = new CubicEase() { EasingMode = EasingMode.EaseInOut };

        /// <summary>
        /// Returns the time until the next instance should be started.
        /// </summary>
        public static TimeSpan AnimateImage(DpiImage tgtImg, Size frameSize, SlideAnimConfig cfg) {
            //fade
            var animFade = new DoubleAnimationUsingKeyFrames();
            animFade.KeyFrames.Add(new EasingDoubleKeyFrame(0d, cfg.Time_Zero));
            animFade.KeyFrames.Add(new EasingDoubleKeyFrame(1d, cfg.Time_FadeInEnd, easeInOut));
            animFade.KeyFrames.Add(new EasingDoubleKeyFrame(1d, cfg.Time_FadeOutBegin));
            animFade.KeyFrames.Add(new EasingDoubleKeyFrame(0d, cfg.Time_End, easeInOut));
            tgtImg.BeginAnimation(UIElement.OpacityProperty, animFade);
            
            //blur
            if (cfg.Blur) {
                tgtImg.Effect = new BlurEffect() { Radius = 0 };
                var animBlur = new DoubleAnimationUsingKeyFrames();
                animBlur.KeyFrames.Add(new EasingDoubleKeyFrame(25d, cfg.Time_Zero));
                animBlur.KeyFrames.Add(new EasingDoubleKeyFrame(0d,  cfg.Time_FadeInEnd, easeInOut));
                animBlur.KeyFrames.Add(new EasingDoubleKeyFrame(0d,  cfg.Time_FadeOutBegin));
                animBlur.KeyFrames.Add(new EasingDoubleKeyFrame(25d, cfg.Time_End, easeInOut));
                tgtImg.Effect.BeginAnimation(BlurEffect.RadiusProperty, animBlur);
            }
            else
                tgtImg.Effect = null;

            switch (cfg.Transition) {
                case SlideTransition.KenBurns:
                    Anim_KBE(tgtImg, frameSize, cfg);
                    break;
                case SlideTransition.Breath:
                    Anim_Breath(tgtImg, frameSize, cfg);
                    break;
                case SlideTransition.Emerge:
                    Anim_Emerge(tgtImg, frameSize, cfg);
                    break;
            }

            return cfg.Time_FadeOutBegin.TimeSpan.Add(cfg.TransitionDelay);
        }

        private static void Anim_KBE(DpiImage tgtImg, Size frameSize, SlideAnimConfig cfg) {
            var zoomIn = ran.Next(2) == 0;
            var panDirPri = ran.Next(2) == 0;//primary axis pan direction
            var imageDur = new Duration(cfg.ImageDuration);

            //zoom animation
            var animZoom = zoomIn ? new DoubleAnimation(1d, 1.2d, imageDur) : new DoubleAnimation(1.2d, 1d, imageDur);
            
            //pan animation
            DoubleAnimation animPanPri;
            DependencyProperty transEdgePri;
            double delta;
            var rCanvas = frameSize.Width / frameSize.Height;
            var rImage = tgtImg.RealSize.Width / tgtImg.RealSize.Height;
            if (rImage >= rCanvas) {
                //width is the longer edge comparing to the size of the canvas
                tgtImg.Height = frameSize.Height;
                tgtImg.Width = tgtImg.Height * rImage;
                transEdgePri = TranslateTransform.XProperty;
                delta = tgtImg.Width - frameSize.Width;
                
                tgtImg.RenderTransformOrigin = new Point(panDirPri ? ran.NextDouble(0.5, 1.0) : ran.NextDouble(0d, 0.5), ran.NextDouble());
                animPanPri = panDirPri ?
                    new DoubleAnimation(-delta + delta * cfg.XPanDistanceR, -delta, imageDur) :
                    new DoubleAnimation(        -delta * cfg.XPanDistanceR,     0d, imageDur);
            }
            else {
                //height is the longer edge comparing to the size of the canvas
                tgtImg.Width = frameSize.Width;
                tgtImg.Height = tgtImg.Width / rImage;
                transEdgePri = TranslateTransform.YProperty;
                delta = tgtImg.Height - frameSize.Height;
                if (cfg.YPanDownOnly && tgtImg.Height > frameSize.Height * cfg.YPanDistanceR)
                    panDirPri = false; //only move down for pics with height larger than YPanDistanceR * screen height after converted to same width as screen
                
                tgtImg.RenderTransformOrigin = new Point(ran.NextDouble(), panDirPri ? ran.NextDouble(0.5, 1.0) : ran.NextDouble(0d, 0.5));
                animPanPri = panDirPri ?
                    new DoubleAnimation(-delta + delta * cfg.YPanDistanceR, -delta, imageDur) :
                    new DoubleAnimation(        -delta * cfg.YPanDistanceR,     0d, imageDur);
            }

            animZoom.FillBehavior = FillBehavior.Stop;
            animPanPri.FillBehavior = FillBehavior.Stop;

            //apply animation
            var tg = (TransformGroup)tgtImg.RenderTransform;
            tg.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, animZoom);
            tg.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, animZoom);
            tg.Children[1].BeginAnimation(transEdgePri, animPanPri);
        }

        public static void Anim_Breath(DpiImage tgtImg, Size frameSize, SlideAnimConfig cfg) {
            var panLeftTop = ran.Next(2) == 0;
            //lastBool1 is used for tracking if the last zoom is in or out
            //zoon animation
            var animZoom = new DoubleAnimationUsingKeyFrames();
            animZoom.KeyFrames.Add(new EasingDoubleKeyFrame(cfg.lastBool1 ?   1d : 1.3d, cfg.Time_Zero));
            animZoom.KeyFrames.Add(new EasingDoubleKeyFrame(cfg.lastBool1 ? 1.3d :   1d, cfg.Time_Mid, easeOut));
            animZoom.KeyFrames.Add(new EasingDoubleKeyFrame(cfg.lastBool1 ? 1.1d : 1.2d, cfg.Time_End, easeIn));
            cfg.lastBool1 = !cfg.lastBool1;

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
                delta = tgtImg.Width - frameSize.Width;
                startPoint = delta * cfg.XPanDistanceR;
                
                tgtImg.RenderTransformOrigin = panLeftTop ? new Point(0d, 0.5d) : new Point(1d, 0.5d);
                animPan = panLeftTop ?
                    new DoubleAnimation(startPoint - delta, !cfg.lastBool1 ? -delta - tgtImg.Width * 0.1d : -delta, new Duration(cfg.ImageDuration)) :
                    new DoubleAnimation(-startPoint,        !cfg.lastBool1 ? tgtImg.Width * 0.1d          : 0d,     new Duration(cfg.ImageDuration));
                //the 0.1 above and below correspond to the 1.1 above
            }
            else {
                //height is the longer edge comparing to the size of the canvas
                tgtImg.Width = frameSize.Width;
                tgtImg.Height = tgtImg.Width / rImage;
                transEdge = TranslateTransform.YProperty;
                delta = tgtImg.Height - frameSize.Height;
                startPoint = delta * cfg.YPanDistanceR;

                if (cfg.YPanDownOnly) panLeftTop = false;
                tgtImg.RenderTransformOrigin = panLeftTop ? new Point(0.5d, 0d) : new Point(0.5d, 1d);
                animPan = panLeftTop ?
                    new DoubleAnimation(startPoint - delta, !cfg.lastBool1 ? -delta - tgtImg.Height * 0.1d : -delta, new Duration(cfg.ImageDuration)) :
                    new DoubleAnimation(-startPoint,        !cfg.lastBool1 ? tgtImg.Height * 0.1d          : 0d,     new Duration(cfg.ImageDuration));
            }

            animZoom.FillBehavior = FillBehavior.Stop;
            animPan.FillBehavior = FillBehavior.Stop;
            animPan.EasingFunction = easeInOut;

            //apply animation
            var tg = (TransformGroup)tgtImg.RenderTransform;
            tg.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, animZoom);
            tg.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, animZoom);
            tg.Children[1].BeginAnimation(transEdge, animPan);
        }

        private static void Anim_Emerge(DpiImage tgtImg, Size frameSize, SlideAnimConfig cfg) {
            var direction = cfg.lastBool1;

            //zoon animation
            var animZoom = new DoubleAnimationUsingKeyFrames();
            animZoom.KeyFrames.Add(new EasingDoubleKeyFrame(0.7d, cfg.Time_Zero));
            animZoom.KeyFrames.Add(new EasingDoubleKeyFrame(  1d, cfg.Time_Mid, easeOut));
            animZoom.KeyFrames.Add(new EasingDoubleKeyFrame(0.7d, cfg.Time_End, easeIn));

            //pan animation
            var animPan = new DoubleAnimationUsingKeyFrames();
            DependencyProperty transEdge;
            var rCanvas = frameSize.Width / frameSize.Height;
            var rImage = tgtImg.RealSize.Width / tgtImg.RealSize.Height;
            if (rImage > rCanvas) {
                //width is the longer edge comparing to the size of the canvas
                tgtImg.Height = frameSize.Height;
                tgtImg.Width = tgtImg.Height * rImage;
                transEdge = TranslateTransform.XProperty;
                tgtImg.RenderTransformOrigin = direction ? new Point(0d, 0.5d) : new Point(1d, 0.5d);

                animPan.KeyFrames.Add(new EasingDoubleKeyFrame(direction ? frameSize.Width : -tgtImg.Width, cfg.Time_Zero));
                animPan.KeyFrames.Add(new SplineDoubleKeyFrame(direction ? -tgtImg.Width : frameSize.Width, cfg.Time_End, new KeySpline(0.2, 0.8, 0.8, 0.2)));
            }
            else {
                //height is the longer edge comparing to the size of the canvas
                tgtImg.Width = frameSize.Width;
                tgtImg.Height = tgtImg.Width / rImage;
                transEdge = TranslateTransform.YProperty;
                tgtImg.RenderTransformOrigin = direction ? new Point(0.5d, 0d) : new Point(0.5d, 1d);

                animPan.KeyFrames.Add(new EasingDoubleKeyFrame(direction ? frameSize.Height : -tgtImg.Height, cfg.Time_Zero));
                animPan.KeyFrames.Add(new SplineDoubleKeyFrame(direction ? -tgtImg.Height : frameSize.Height, cfg.Time_End, new KeySpline(0.2, 0.8, 0.8, 0.2)));
            }

            animZoom.FillBehavior = FillBehavior.Stop;
            animPan.FillBehavior = FillBehavior.Stop;

            //apply animation
            var tg = (TransformGroup)tgtImg.RenderTransform;
            tg.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, animZoom);
            tg.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, animZoom);
            tg.Children[1].BeginAnimation(transEdge, animPan);
        }
    }
}
