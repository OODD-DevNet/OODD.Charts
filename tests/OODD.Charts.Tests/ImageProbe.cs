using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 像素扫描器（T60）。验收判据全部落在"渲染产物看起来对不对"这一层，
    /// 所以这里只用 Skia 把 PNG 读回裸像素，不引任何第三方图像库。
    /// <para>
    /// 统一按 BGRA8888 预乘读取：与 <c>Chart.RenderImage</c> 的输出格式一致，
    /// 索引 <c>y*Width*4 + x*4</c> 分别是 B、G、R、A。
    /// </para>
    /// </summary>
    internal sealed class ImageProbe : IDisposable
    {
        private readonly byte[] _pixels;
        private readonly string? _tempPath;

        private ImageProbe(byte[] pixels, int width, int height, string? tempPath)
        {
            _pixels = pixels;
            Width = width;
            Height = height;
            _tempPath = tempPath;
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>把 PNG 字节写进临时文件再解码；Skia 3.116 只有按路径/流的解码入口。</summary>
        public static ImageProbe FromPng(byte[] png)
        {
            string path = Path.Combine(Path.GetTempPath(), "oodd-charts-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, png);
            try
            {
                return FromFile(path);
            }
            catch
            {
                File.Delete(path);
                throw;
            }
        }

        public static ImageProbe FromFile(string path)
        {
            using (SKImage? image = SKImage.FromEncodedData(path))
            {
                if (image == null)
                {
                    throw new InvalidDataException("无法解码图像: " + path);
                }

                SKImageInfo info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                byte[] buffer = new byte[info.BytesSize];
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    if (!image.ReadPixels(info, handle.AddrOfPinnedObject(), info.RowBytes, 0, 0))
                    {
                        throw new InvalidDataException("ReadPixels 失败: " + path);
                    }
                }
                finally
                {
                    handle.Free();
                }

                return new ImageProbe(buffer, image.Width, image.Height, null);
            }
        }

        public SKColor Pixel(int x, int y)
        {
            int index = y * Width * 4 + x * 4;
            return new SKColor(_pixels[index + 2], _pixels[index + 1], _pixels[index], _pixels[index + 3]);
        }

        /// <summary>非白：任一通道离 255 超过 8，容住抗锯齿边缘与浅灰网格。</summary>
        public bool IsInk(int x, int y)
        {
            int index = y * Width * 4 + x * 4;
            return _pixels[index] < 247 || _pixels[index + 1] < 247 || _pixels[index + 2] < 247;
        }

        public bool HasInkInBox(int left, int top, int right, int bottom)
        {
            for (int y = Math.Max(0, top); y < Math.Min(Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(right, Width); x++)
                {
                    if (IsInk(x, y))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>矩形内非白像素占比，用于 A2"字段所在格看得见"。</summary>
        public double NonWhiteRatio(SKRect rect)
        {
            int left = Math.Max(0, (int)Math.Ceiling(rect.Left));
            int top = Math.Max(0, (int)Math.Ceiling(rect.Top));
            int right = Math.Min(Width, (int)Math.Floor(rect.Right));
            int bottom = Math.Min(Height, (int)Math.Floor(rect.Bottom));
            int area = (right - left) * (bottom - top);
            if (area <= 0)
            {
                return 0d;
            }

            int ink = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (IsInk(x, y))
                    {
                        ink++;
                    }
                }
            }
            return (double)ink / area;
        }

        /// <summary>矩形内所有非白像素的平均色，用来判红蓝约定（A3）。</summary>
        public SKColor AverageInkColor(SKRect rect)
        {
            int left = Math.Max(0, (int)Math.Floor(rect.Left));
            int top = Math.Max(0, (int)Math.Floor(rect.Top));
            int right = Math.Min(Width, (int)Math.Ceiling(rect.Right));
            int bottom = Math.Min(Height, (int)Math.Ceiling(rect.Bottom));

            long red = 0;
            long green = 0;
            long blue = 0;
            int count = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (!IsInk(x, y))
                    {
                        continue;
                    }
                    int index = y * Width * 4 + x * 4;
                    blue += _pixels[index];
                    green += _pixels[index + 1];
                    red += _pixels[index + 2];
                    count++;
                }
            }

            if (count == 0)
            {
                return SKColors.Transparent;
            }
            return new SKColor((byte)(red / count), (byte)(green / count), (byte)(blue / count));
        }

        /// <summary>沿某一列扫描，返回每段连续墨迹的中心纵坐标（即横线位置）。</summary>
        public IReadOnlyList<double> HorizontalRulesAt(int x, double fromY, double toY)
        {
            return Runs(alongX: false, fixedIndex: x, from: (int)fromY, to: (int)toY);
        }

        /// <summary>沿某一行扫描，返回每段连续墨迹的中心横坐标（即竖线位置）。</summary>
        public IReadOnlyList<double> VerticalRulesAt(int y, double fromX, double toX)
        {
            return Runs(alongX: true, fixedIndex: y, from: (int)fromX, to: (int)toX);
        }

        private IReadOnlyList<double> Runs(bool alongX, int fixedIndex, int from, int to)
        {
            List<double> centers = new List<double>();
            int runStart = -1;
            int limit = alongX ? Width : Height;
            int stop = Math.Min(to, limit - 1);

            for (int i = Math.Max(from, 0); i <= stop; i++)
            {
                bool ink = alongX ? IsInk(i, fixedIndex) : IsInk(fixedIndex, i);
                if (ink && runStart < 0)
                {
                    runStart = i;
                }
                else if (!ink && runStart >= 0)
                {
                    centers.Add((runStart + i - 1) / 2d);
                    runStart = -1;
                }
            }
            if (runStart >= 0)
            {
                centers.Add((runStart + stop) / 2d);
            }
            return centers;
        }

        /// <summary>相邻间距列表，四舍五入到 0.1px，方便直接断言 15/20/30/75/120。</summary>
        public static IReadOnlyList<double> Gaps(IReadOnlyList<double> positions)
        {
            List<double> gaps = new List<double>();
            for (int i = 1; i < positions.Count; i++)
            {
                gaps.Add(Math.Round(positions[i] - positions[i - 1], 1));
            }
            return gaps;
        }

        /// <summary>
        /// 跨区间逐行统计墨迹占比，返回"几乎贯通"的行中心（即真正的格线）。
        /// 按覆盖率而非单点判据，字、符号、曲线都不会被误认成线。
        /// </summary>
        public IReadOnlyList<double> StrongHorizontalLines(double fromY, double toY, double fromX, double toX, double minCoverage)
        {
            int left = Math.Max(0, (int)fromX);
            int right = Math.Min(Width, (int)toX);
            int span = Math.Max(1, right - left);

            List<double> lines = new List<double>();
            int runStart = -1;
            for (int y = Math.Max(0, (int)fromY); y <= Math.Min(toY, Height - 1); y++)
            {
                int ink = 0;
                for (int x = left; x < right; x++)
                {
                    if (IsInk(x, y))
                    {
                        ink++;
                    }
                }

                bool line = (double)ink / span >= minCoverage;
                if (line && runStart < 0)
                {
                    runStart = y;
                }
                else if (!line && runStart >= 0)
                {
                    lines.Add((runStart + y - 1) / 2d);
                    runStart = -1;
                }
            }
            if (runStart >= 0)
            {
                lines.Add((runStart + Math.Min(toY, Height - 1)) / 2d);
            }
            return lines;
        }

        /// <summary>竖线版判据，同 <see cref="StrongHorizontalLines"/>。</summary>
        public IReadOnlyList<double> StrongVerticalLines(double fromX, double toX, double fromY, double toY, double minCoverage)
        {
            int top = Math.Max(0, (int)fromY);
            int bottom = Math.Min(Height, (int)toY);
            int span = Math.Max(1, bottom - top);

            List<double> lines = new List<double>();
            int runStart = -1;
            for (int x = Math.Max(0, (int)fromX); x <= Math.Min(toX, Width - 1); x++)
            {
                int ink = 0;
                for (int y = top; y < bottom; y++)
                {
                    if (IsInk(x, y))
                    {
                        ink++;
                    }
                }

                bool line = (double)ink / span >= minCoverage;
                if (line && runStart < 0)
                {
                    runStart = x;
                }
                else if (!line && runStart >= 0)
                {
                    lines.Add((runStart + x - 1) / 2d);
                    runStart = -1;
                }
            }
            if (runStart >= 0)
            {
                lines.Add((runStart + Math.Min(toX, Width - 1)) / 2d);
            }
            return lines;
        }

        /// <summary>
        /// 红色墨。产程图的参考线、异常圈注都是纯红（255,0,0）叠在白底/黑线上，
        /// 抗锯齿最淡的一档也远好于"红得分量明显压过绿蓝"这条线。
        /// </summary>
        public bool IsRedInk(int x, int y)
        {
            int index = y * Width * 4 + x * 4;
            int blue = _pixels[index];
            int green = _pixels[index + 1];
            int red = _pixels[index + 2];
            return red > 120 && red - green > 50 && red - blue > 50;
        }

        /// <summary>矩形内是否存在红色墨。</summary>
        public bool HasRedInBox(int left, int top, int right, int bottom)
        {
            for (int y = Math.Max(0, top); y < Math.Min(Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(right, Width); x++)
                {
                    if (IsRedInk(x, y))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>某一行里所有红色段的中心 x，用于把斜参考线在给定高度上的落点读出来。</summary>
        public IReadOnlyList<double> RedRunsInRow(int y, double fromX, double toX)
        {
            List<double> centers = new List<double>();
            int left = Math.Max(0, (int)Math.Ceiling(fromX));
            int right = Math.Min(Width - 1, (int)Math.Floor(toX));
            int runStart = -1;
            for (int x = left; x <= right; x++)
            {
                bool red = IsRedInk(x, y);
                if (red && runStart < 0)
                {
                    runStart = x;
                }
                else if (!red && runStart >= 0)
                {
                    centers.Add((runStart + x - 1) / 2d);
                    runStart = -1;
                }
            }
            if (runStart >= 0)
            {
                centers.Add((runStart + right) / 2d);
            }
            return centers;
        }

        public void Dispose()
        {
            if (_tempPath != null && File.Exists(_tempPath))
            {
                File.Delete(_tempPath);
            }
        }
    }
}
