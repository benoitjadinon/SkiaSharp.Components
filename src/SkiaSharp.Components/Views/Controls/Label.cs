﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace SkiaSharp.Components
{
    public class Label : View, IMeasurable
    {
        #region Constants

        public static readonly IBrush DefaultForegroundBrush = new ColorBrush(SKColors.Black);

        public const float DefaultTextSize = 20;

        #endregion

        #region Fields

        private IEnumerable<Span> spans;

        private float? lineHeight;

        private Span style = new Span()
        {
            TextSize = DefaultTextSize,
        };

        private Alignment horizontalAlignment;
        private Alignment verticalAlignment;

        private Dictionary<Span, SKRect> spanLayout;

        #endregion

        #region Properties

        public Alignment HorizontalAlignment 
        {
            get => this.horizontalAlignment;
            set => this.SetAndInvalidate(ref this.horizontalAlignment, value);
        }

        public Alignment VerticalAlignment 
        {
            get => this.verticalAlignment;
            set => this.SetAndInvalidate(ref this.verticalAlignment, value);
        }

        public IEnumerable<Span> Spans 
        {
            get => this.spans;
            set
            {
                this.NeedsLayout = true;
                this.SetAndInvalidate(ref this.spans, value);
            }
        }

        public IBrush Foreground
        {
            get => this.style.Foreground;
            set => this.SetAndInvalidate(() => this.Foreground, (v) => this.style.Foreground = v, value);
        }

        public SKTypeface Typeface
        {
            get => this.style.Typeface;
            set => this.SetAndInvalidate(() => this.Typeface, (v) => this.style.Typeface = v, value);
        }

        public float TextSize
        {
            get => this.style.TextSize;
            set => this.SetAndInvalidate(() => this.TextSize, (v) => this.style.TextSize = v, value);
        }

        public float LineHeight
        {
            get => this.lineHeight ?? this.TextSize * 1.15f;
            set => this.SetAndInvalidate(ref this.lineHeight, value);
        }

        public string Text
        {
            get => string.Join("", this.Spans?.Select(x => x.Text) ?? new string[0]);
            set => this.Spans = new[]
            {
                new Span(this.style)
                {
                    Text = value,
                }
            };
        }

        #endregion

        public override void Layout(SKRect available)
        {
            var splitSpans = SplitLines(this.Spans, available.Size, this.LineHeight * Density.Global, out SKSize totalSize);

            var offset = SKPoint.Empty;

            if (this.HorizontalAlignment == Alignment.Center)
            {
                offset.X = available.Width / 2 - totalSize.Width / 2;
            }
            else if (this.HorizontalAlignment == Alignment.End)
            {
                offset.X = available.Width - totalSize.Width;
            }

            if (this.VerticalAlignment == Alignment.Center)
            {
                offset.Y = available.Height / 2 - totalSize.Height / 2;
            }
            else if (this.VerticalAlignment == Alignment.End)
            {
                offset.Y = available.Height - totalSize.Height;
            }

            spanLayout = new Dictionary<Span, SKRect>();

            foreach (var span in splitSpans)
            {
                var area = SKRect.Create(offset.X + available.Left + span.LayoutFrame.Left - span.Bounds.Left, offset.Y + available.Top + span.LayoutFrame.Top, span.LayoutFrame.Width, span.LayoutFrame.Height);
                spanLayout[span] = area;
            }

            base.Layout(SKRect.Create(available.Left, available.Top, totalSize.Width, totalSize.Height));
        }

        public override void Render(SKCanvas canvas)
        {
            base.Render(canvas);

            if(spanLayout != null)
            {
                foreach (var spanKvp in spanLayout)
                {
                    (style.Foreground ?? spanKvp.Key.Foreground).Text(canvas, 
                        spanKvp.Key.Text, 
                        spanKvp.Value,
                        style.Typeface ?? spanKvp.Key.Typeface, 
                        style.TextSize >= 0 ? style.TextSize : spanKvp.Key.TextSize, 
                        spanKvp.Key.Decorations
                    );
                } 
            }
        }

        public SKSize Measure(SKSize area)
        {
            var spans = SplitLines(this.Spans, area, this.LineHeight * Density.Global, out SKSize size);
            return size;
        }

        private static IEnumerable<Span> Split(IEnumerable<Span> spans, char c)
        {
            return spans.SelectMany(r =>
            {
                if (r.Text == null)
                    return new Span[0];

                var returns = r.Text.Split(new[] { c }, StringSplitOptions.None);
                return returns.SelectMany((s, i) =>
                {
                    var result = new List<Span>();
                    if (i > 0)
                    {
                        result.Add(new Span(r)
                        {
                            Text = c.ToString(),
                        });
                    }
                    if (!string.IsNullOrEmpty(s))
                    {
                        result.Add(new Span(r)
                        {
                            Text = s,
                        });
                    }
                    return result;
                });
            });
        }

        private static float UpdateLineHeight(int line, List<Span> spans, float lineHeight)
        {
            if (line == 0)
            {
                var height = spans.Max(s => -s.Bounds.Top);
                foreach (var span in spans)
                {
                    var f = span.LayoutFrame;
                    span.LayoutFrame = SKRect.Create(f.Left, f.Top, f.Width, height);
                }
                return height;
            }

            return lineHeight;
        }

        private static Span[] SplitLines(IEnumerable<Span> text, SKSize frame, float lineHeight, out SKSize size)
        {
            if (text == null)
            {
                size = SKSize.Empty;
                return new Span[0];
            }

            // splittingLines
            var spans = Split(text, '\n');

            // Splitting words
            spans = Split(spans, ' ').ToList();

            var updatedSpans = new List<Span>();

            float y = 0, x = 0;
            SKRect bounds = SKRect.Empty;
            int line = 0;

            foreach (var span in spans)
            {
                using (var paint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    TextAlign = SKTextAlign.Left,
                    Typeface = span.Typeface,
                    FakeBoldText = span.Decorations.HasFlag(TextDecoration.Bold),
                    TextSize = span.TextSize * Density.Global,
                })
                {
                    var previousLine = line;

                    if (span.Text == "\n")
                    {
                        var newLineHeight = UpdateLineHeight(line, updatedSpans, lineHeight);
                        line++;
                        x = 0;
                        y += newLineHeight;
                    }
                    else if (span.Text == " ")
                    {
                        x += paint.MeasureText(span.Text);
                    }
                    else if (span.Text != null)
                    {
                        if (span.Text.Length > 0)
                        {
                            paint.MeasureText(span.Text, ref bounds);

                            var shouldReturn = x > 0 && x + bounds.Width - bounds.Left > frame.Width + 1;

                            if (shouldReturn)
                            {
                                var newLineHeight = UpdateLineHeight(line, updatedSpans, lineHeight);
                                line++;
                                x = 0;
                                y += newLineHeight;
                            }

                            updatedSpans.Add(new Span()
                            {
                                Text = span.Text,
                                Foreground = span.Foreground,
                                TextSize = span.TextSize,
                                Line = line,
                                Typeface = span.Typeface,
                                Bounds = bounds,
                                LayoutFrame = SKRect.Create(x, y, bounds.Width - bounds.Left, lineHeight),
                            });

                            x += bounds.Width;
                        }
                    }
                }
            }

            if (line == 0)
            {
                UpdateLineHeight(line, updatedSpans, lineHeight);
            }

            var result = updatedSpans.ToArray();

            // Total size
            var h = result.Length > 0 ? result.Max(s => s.LayoutFrame.Bottom) - result.Min(s => s.LayoutFrame.Top) : 0;
            var w = result.Length > 0 ? result.Max(s => s.LayoutFrame.Right) - result.Min(s => s.LayoutFrame.Left) : 0;
            size = new SKSize(w, h);

            return result;
        }
    }
}
