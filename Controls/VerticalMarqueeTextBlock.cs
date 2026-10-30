using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Panak.Controls {
    /// <summary>
    /// Displays a file or folder name. If the text fits in 1 or 2 lines, it is shown statically
    /// with complete two-line visibility. If the text exceeds 2 lines, it becomes a vertical
    /// marquee that scrolls smoothly when the mouse hovers over it.
    /// </summary>
    public class VerticalMarqueeTextBlock : ContentControl {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(VerticalMarqueeTextBlock),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTextPropertyChanged));

        public static readonly DependencyProperty IsHoveredProperty =
            DependencyProperty.Register(nameof(IsHovered), typeof(bool), typeof(VerticalMarqueeTextBlock),
                new FrameworkPropertyMetadata(false, OnIsHoveredPropertyChanged));

        public string Text {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsHovered {
            get => (bool)GetValue(IsHoveredProperty);
            set => SetValue(IsHoveredProperty, value);
        }

        private readonly Grid _containerGrid;
        private readonly StackPanel _stackPanel;
        private readonly TranslateTransform _transform;
        private readonly TextBlock _primaryTextBlock;
        private readonly TextBlock _duplicateTextBlock;
        private readonly TextBlock _probeTextBlock;

        private bool _isMultiLineExceeded = false;
        private double _measuredTextHeight = 0.0;
        private const double ViewportHeightConstant = 34.0;
        private const double TextBlockWidthConstant = 66.0;

        public bool IsMultiLineExceeded => _isMultiLineExceeded;

        public VerticalMarqueeTextBlock() {
            Focusable = false;
            ClipToBounds = true;
            Width = TextBlockWidthConstant;
            Height = ViewportHeightConstant;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Top;

            _transform = new TranslateTransform(0, 0);

            _primaryTextBlock = new TextBlock {
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Width = TextBlockWidthConstant,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };

            _duplicateTextBlock = new TextBlock {
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Width = TextBlockWidthConstant,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed
            };

            _probeTextBlock = new TextBlock {
                Text = "Tg",
                TextWrapping = TextWrapping.NoWrap
            };

            _primaryTextBlock.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
            _primaryTextBlock.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(FontSize)) { Source = this });
            _primaryTextBlock.SetBinding(TextBlock.FontFamilyProperty, new Binding(nameof(FontFamily)) { Source = this });
            _primaryTextBlock.SetBinding(TextBlock.FontWeightProperty, new Binding(nameof(FontWeight)) { Source = this });
            _primaryTextBlock.SetBinding(TextBlock.FontStyleProperty, new Binding(nameof(FontStyle)) { Source = this });

            _duplicateTextBlock.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
            _duplicateTextBlock.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(FontSize)) { Source = this });
            _duplicateTextBlock.SetBinding(TextBlock.FontFamilyProperty, new Binding(nameof(FontFamily)) { Source = this });
            _duplicateTextBlock.SetBinding(TextBlock.FontWeightProperty, new Binding(nameof(FontWeight)) { Source = this });
            _duplicateTextBlock.SetBinding(TextBlock.FontStyleProperty, new Binding(nameof(FontStyle)) { Source = this });

            _stackPanel = new StackPanel {
                Orientation = Orientation.Vertical,
                Width = TextBlockWidthConstant,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = _transform
            };

            _stackPanel.Children.Add(_primaryTextBlock);
            _stackPanel.Children.Add(_duplicateTextBlock);

            _containerGrid = new Grid {
                ClipToBounds = true,
                Width = TextBlockWidthConstant,
                Height = ViewportHeightConstant,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            _containerGrid.Children.Add(_stackPanel);

            Content = _containerGrid;

            Loaded += (s, e) => UpdateTextMetricsAndState();
            Unloaded += (s, e) => StopMarqueeAnimation(instant: true);
            IsVisibleChanged += (s, e) => {
                if (!IsVisible) {
                    StopMarqueeAnimation(instant: true);
                } else if (_isMultiLineExceeded && ShouldAnimate()) {
                    StartMarqueeAnimation();
                }
            };

            MouseEnter += (s, e) => CheckHoverStateChanged();
            MouseLeave += (s, e) => CheckHoverStateChanged();
        }

        private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is VerticalMarqueeTextBlock control) {
                control.UpdateTextMetricsAndState();
            }
        }

        private static void OnIsHoveredPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is VerticalMarqueeTextBlock control) {
                control.CheckHoverStateChanged();
            }
        }

        private bool ShouldAnimate() {
            return (IsHovered || IsMouseOver) && IsVisible && IsLoaded && _isMultiLineExceeded;
        }

        private void CheckHoverStateChanged() {
            if (!_isMultiLineExceeded) {
                StopMarqueeAnimation(instant: true);
                return;
            }

            if (ShouldAnimate()) {
                StartMarqueeAnimation();
            } else {
                StopMarqueeAnimation(instant: false);
            }
        }

        /// <summary>
        /// Inserts zero-width break opportunities (\u200B) into filenames
        /// with dots, underscores, dashes, or long unbroken character sequences
        /// so WPF TextWrapping can wrap cleanly across lines.
        /// </summary>
        public static string PrepareWrappableText(string? text) {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder(text.Length + 16);
            int charsSinceBreak = 0;

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                sb.Append(c);
                charsSinceBreak++;

                if (char.IsWhiteSpace(c)) {
                    charsSinceBreak = 0;
                    continue;
                }

                // Break opportunity after separator characters
                if (c == '_' || c == '.' || c == '-' || c == '/' || c == '\\') {
                    if (i + 1 < text.Length && text[i + 1] != '\u200B' && !char.IsWhiteSpace(text[i + 1])) {
                        sb.Append('\u200B');
                        charsSinceBreak = 0;
                    }
                    continue;
                }

                // CamelCase break opportunity (lowercase followed by uppercase)
                if (char.IsLower(c) && i + 1 < text.Length && char.IsUpper(text[i + 1])) {
                    sb.Append('\u200B');
                    charsSinceBreak = 0;
                    continue;
                }

                // Unbroken continuous run of characters (e.g. very long word or hash)
                if (charsSinceBreak >= 8 && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1])) {
                    sb.Append('\u200B');
                    charsSinceBreak = 0;
                }
            }

            return sb.ToString();
        }

        public void UpdateTextMetricsAndState() {
            string raw = Text ?? string.Empty;
            string wrappable = PrepareWrappableText(raw);

            _primaryTextBlock.Text = wrappable;
            _duplicateTextBlock.Text = wrappable;

            _probeTextBlock.FontSize = FontSize;
            _probeTextBlock.FontFamily = FontFamily;
            _probeTextBlock.FontWeight = FontWeight;
            _probeTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double singleLineHeight = Math.Max(14.0, _probeTextBlock.DesiredSize.Height);

            // 2 lines with safety margin for ascenders/descenders
            double twoLinesThreshold = singleLineHeight * 2.15;

            _primaryTextBlock.Measure(new Size(TextBlockWidthConstant, double.PositiveInfinity));
            _measuredTextHeight = _primaryTextBlock.DesiredSize.Height;

            _isMultiLineExceeded = _measuredTextHeight > twoLinesThreshold;

            if (!_isMultiLineExceeded) {
                StopMarqueeAnimation(instant: true);
                _duplicateTextBlock.Visibility = Visibility.Collapsed;
            } else {
                if (ShouldAnimate()) {
                    StartMarqueeAnimation();
                } else {
                    StopMarqueeAnimation(instant: true);
                }
            }
        }

        private void StartMarqueeAnimation() {
            if (!_isMultiLineExceeded || _measuredTextHeight <= ViewportHeightConstant) return;

            // In ping-pong / reverse scrolling mode, duplicate text is not needed
            _duplicateTextBlock.Visibility = Visibility.Collapsed;

            double distanceToBottom = Math.Max(0.0, _measuredTextHeight - ViewportHeightConstant);
            if (distanceToBottom <= 0.5) return;

            const double speed = 20.0; // pixels per second
            double scrollDuration = Math.Max(0.6, distanceToBottom / speed);

            var anim = new DoubleAnimationUsingKeyFrames();
            Timeline.SetDesiredFrameRate(anim, 30);

            // 1. Pause at top (0.8s) so user sees line 1-2
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8))));

            // 2. Scroll smoothly to bottom
            double tBottom = 0.8 + scrollDuration;
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-distanceToBottom, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(tBottom))));

            // 3. Pause at bottom (1.2s) to read file extension/ending
            double tBottomPause = tBottom + 1.2;
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(-distanceToBottom, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(tBottomPause))));

            // 4. Scroll smoothly back up to top (reverse scroll)
            double tTop = tBottomPause + scrollDuration;
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(tTop))));

            // 5. Pause at top (0.8s) before starting next scroll cycle
            double tCycleEnd = tTop + 0.8;
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(tCycleEnd))));

            anim.RepeatBehavior = RepeatBehavior.Forever;

            _transform.BeginAnimation(TranslateTransform.YProperty, anim);
        }

        private void StopMarqueeAnimation(bool instant) {
            if (instant) {
                _transform.BeginAnimation(TranslateTransform.YProperty, null);
                _transform.Y = 0;
                _duplicateTextBlock.Visibility = Visibility.Collapsed;
            } else {
                // Smoothly return to top Y = 0 on mouse leave
                _duplicateTextBlock.Visibility = Visibility.Collapsed;
                double currentY = _transform.Y;
                if (Math.Abs(currentY) > 0.5) {
                    var returnAnim = new DoubleAnimation(currentY, 0.0, TimeSpan.FromMilliseconds(180)) {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    returnAnim.Completed += (s, e) => {
                        _transform.BeginAnimation(TranslateTransform.YProperty, null);
                        _transform.Y = 0;
                    };
                    _transform.BeginAnimation(TranslateTransform.YProperty, returnAnim);
                } else {
                    _transform.BeginAnimation(TranslateTransform.YProperty, null);
                    _transform.Y = 0;
                }
            }
        }
    }
}
