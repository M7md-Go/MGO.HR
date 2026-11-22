using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HR.UI.UI
{
    /// <summary>
    /// Lightweight RTL bar chart (pure GDI, zero new references — kept for the
    /// 4.0 Client Profile). Categories are drawn right-to-left; value labels sit
    /// above each bar; category labels below. Set the series, then Invalidate.
    /// </summary>
    public class BarChartPanel : Panel
    {
        private string _title;
        private readonly List<string> _categories = new List<string>();
        private readonly List<float> _values = new List<float>();
        private Color _barColor;
        private string _emptyText = "لا توجد بيانات";

        public BarChartPanel()
        {
            _barColor = Color.FromArgb(43, 87, 154);
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        public string ChartTitle
        {
            get { return _title; }
            set { _title = value; Invalidate(); }
        }

        public Color BarColor
        {
            get { return _barColor; }
            set { _barColor = value; Invalidate(); }
        }

        public string EmptyText
        {
            get { return _emptyText; }
            set { _emptyText = value; Invalidate(); }
        }

        /// <summary>Replaces the whole series in one call.</summary>
        public void SetSeries(List<KeyValuePair<string, float>> points)
        {
            _categories.Clear();
            _values.Clear();
            if (points != null)
            {
                foreach (KeyValuePair<string, float> point in points)
                {
                    _categories.Add(point.Key ?? string.Empty);
                    _values.Add(point.Value);
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            Graphics graphics = args.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (Font titleFont = Theme.Font(10f))
            using (Font valueFont = Theme.Font(7.5f))
            using (Font labelFont = Theme.Font(7.5f))
            {
                int top = 8;
                if (!string.IsNullOrEmpty(_title))
                {
                    using (StringFormat center = new StringFormat())
                    {
                        center.Alignment = StringAlignment.Center;
                        center.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(60, 72, 90)))
                        {
                            graphics.DrawString(_title, titleFont, brush, new RectangleF(0f, top, Width, 22f), center);
                        }
                    }
                    top += 26;
                }

                int plotBottom = Height - 26;
                int plotTop = top + 14;
                int plotHeight = plotBottom - plotTop;
                if (plotHeight <= 4 || _categories.Count == 0)
                {
                    if (_categories.Count == 0)
                    {
                        using (StringFormat center = new StringFormat())
                        {
                            center.Alignment = StringAlignment.Center;
                            center.LineAlignment = StringAlignment.Center;
                            center.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                            using (SolidBrush hint = new SolidBrush(Color.FromArgb(150, 158, 170)))
                            {
                                graphics.DrawString(string.IsNullOrEmpty(_emptyText) ? "لا توجد بيانات" : _emptyText,
                                    labelFont, hint, ClientRectangle, center);
                            }
                        }
                    }
                    return;
                }

                float maxValue = 0f;
                foreach (float value in _values)
                {
                    if (value > maxValue)
                    {
                        maxValue = value;
                    }
                }
                if (maxValue <= 0f)
                {
                    maxValue = 1f;
                }

                // RTL: the first category sits at the right edge
                float slotWidth = (float)Width / _categories.Count;
                float barWidth = Math.Min(38f, slotWidth * 0.6f);
                using (SolidBrush bar = new SolidBrush(_barColor))
                using (SolidBrush valueText = new SolidBrush(Color.FromArgb(60, 72, 90)))
                using (SolidBrush labelText = new SolidBrush(Color.FromArgb(110, 120, 135)))
                {
                    for (int i = 0; i < _categories.Count; i++)
                    {
                        float slotRight = Width - slotWidth * i;
                        float x = slotRight - slotWidth / 2f - barWidth / 2f;
                        float barHeight = (float)plotHeight * (_values[i] / maxValue);
                        RectangleF barRect = new RectangleF(x, plotBottom - barHeight, barWidth, barHeight);
                        if (barHeight >= 1f)
                        {
                            graphics.FillRectangle(bar, barRect);
                        }
                        StringFormat center = new StringFormat();
                        center.Alignment = StringAlignment.Center;
                        center.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                        string valueLabel = _values[i] % 1f == 0f
                            ? ((int)_values[i]).ToString()
                            : _values[i].ToString("0.0");
                        using (StringFormat locked = center)
                        {
                            graphics.DrawString(valueLabel, valueFont, valueText,
                                new RectangleF(x - 10f, barRect.Top - 16f, barWidth + 20f, 14f), locked);
                        }
                        graphics.DrawString(_categories[i], labelFont, labelText,
                            new RectangleF(slotRight - slotWidth - 4f, plotBottom + 2f, slotWidth, 15f), center);
                        center.Dispose();
                    }
                }

                using (Pen axis = new Pen(Color.FromArgb(210, 218, 228)))
                {
                    graphics.DrawLine(axis, 2f, plotBottom, Width - 2f, plotBottom);
                }
            }
        }
    }
}
