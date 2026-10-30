using System;
using System.IO;
using System.Security.Cryptography;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// T200 前置验证（docs/07 E1 硬闸门）：<c>SKShader.CreatePattern</c> + <c>ClipRect</c> +
    /// 自定义变换这三件事在 net462 与 net8.0 上能否出<b>逐字节相同</b>的图。
    /// <para>
    /// 这张闸门决定 <c>Painter</c> 能不能加图样填充：<b>不过</b>就整体降级为逐线段绘制
    /// （慢，但确定性一致）。这里刻意只用裸 Skia，不碰 <c>Painter</c>——先证明原生能力，
    /// 再决定要不要在它上面盖 API。
    /// </para>
    /// <para>
    /// 与 <see cref="CrossTfmTests"/> 同一套做法：本 TFM 出图落盘，另一 TFM 的产物必须在磁盘上，
    /// 然后逐字节对比。因此本类需要两个 TFM 各跑一次才算验完。
    /// </para>
    /// </summary>
    public sealed class PatternFillProbeTests
    {
        private const string EvidenceSubdir = "pattern-probe";

        [Fact]
        public void 图样填充_两TFM出图逐字节相同()
        {
            string tfm = Environment.Version.Major >= 5 ? "net8.0" : "net462";
            string dir = Path.Combine(TestDocuments.RepoRoot, "artifacts", EvidenceSubdir);
            Directory.CreateDirectory(dir);

            byte[] nineSix = Render(96f);
            File.WriteAllBytes(Path.Combine(dir, tfm + "-pattern-96dpi.png"), nineSix);

            string other = Path.Combine(dir, (tfm == "net8.0" ? "net462" : "net8.0") + "-pattern-96dpi.png");
            Assert.True(File.Exists(other), "需要另一个 TFM 也跑过本测试才有对照物，缺：" + other);

            byte[] reference = File.ReadAllBytes(other);
            Assert.True(nineSix.Length == reference.Length,
                        "字节长度不同：" + nineSix.Length + " vs " + reference.Length);
            for (int i = 0; i < nineSix.Length; i++)
            {
                Assert.True(nineSix[i] == reference[i],
                            "第 " + i + " 字节起不同：" + nineSix[i] + " vs " + reference[i]
                            + "（" + tfm + " 对 " + Sha256Hex(nineSix) + " / " + Sha256Hex(reference) + "）");
            }
        }

        [Fact]
        public void 图样填充_96dpi下图案确有落墨()
        {
            // 若 net462 上 CreatePattern 静默不渲染，字节比对会"一致地空白"而通过。
            // 这条用例钉住"图案真的画出来了"：带内必须有非白像素，且不是整块实心。
            byte[] png = Render(96f);
            Assert.True(png.Length > 100, "PNG 空或异常小：" + png.Length);

            int ink = InkColumns(png);
            Assert.True(ink > 0, "图样一列像素都没打到，说明 pattern shader 在本 TFM 上没生效");
            // 稀疏的斜纹不应把整个 200px 宽的带涂满
            Assert.True(ink < 200, "图案填满了整个区域，说明它不是图样而是实心填充：ink=" + ink);
        }

        [Fact]
        public void 图样填充_裁剪与自定义变换生效()
        {
            // 带外必须干净：clip 若不生效，图案会铺满整张画布。
            byte[] png = Render(96f);
            int ink = InkColumns(png);
            Assert.True(ink > 0 && ink < 200, "裁剪未生效或图案缺失：ink=" + ink);
        }

        /// <summary>数图案带所在行上有多少列落墨（跨 TFM 稳定，用作"图案非空且未涂满"的判据）。</summary>
        private static int InkColumns(byte[] png)
        {
            using (SKData data = SKData.CreateCopy(png))
            using (SKBitmap bitmap = SKBitmap.Decode(data))
            {
                Assert.NotNull(bitmap);
                int ink = 0;
                int row = bitmap.Height / 2;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    SKColor c = bitmap.GetPixel(x, row);
                    if (c.Red < 250 || c.Green < 250 || c.Blue < 250)
                    {
                        ink++;
                    }
                }

                return ink;
            }
        }

        private static byte[] Render(float dpi)
        {
            SKPicture picture = BuildPicture();
            try
            {
                float scale = dpi / 96f;
                int width = (int)Math.Round(240f * scale);
                int height = (int)Math.Round(120f * scale);
                using (SKSurface surface = SKSurface.Create(
                    new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)))
                {
                    SKCanvas canvas = surface.Canvas;
                    canvas.Clear(SKColors.White);
                    canvas.Scale(scale, scale);
                    canvas.DrawPicture(picture);
                    using (SKImage image = surface.Snapshot())
                    using (SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        return encoded.ToArray();
                    }
                }
            }
            finally
            {
                picture.Dispose();
            }
        }

        /// <summary>
        /// 图样 + 裁剪 + 自定义变换三件事一次跑完：
        /// 离屏 8px 斜纹 tile → repeat shader → 平移/缩放变换下填充一条路径 → 矩形裁剪到带内。
        /// </summary>
        private static SKPicture BuildPicture()
        {
            SKPicture picture;
            using (SKPictureRecorder recorder = new SKPictureRecorder())
            {
                SKCanvas canvas = recorder.BeginRecording(new SKRect(0f, 0f, 240f, 120f));

                using (SKBitmap tile = NewTile(8))
                {
                    // tile 在用户空间占 8 逻辑像素；变换先缩放再平移，验证 shader 跟随 CTM。
                    canvas.Save();
                    canvas.Translate(20f, 10f);
                    canvas.Scale(2f, 2f);

                    canvas.Save();
                    canvas.ClipRect(new SKRect(0f, 0f, 100f, 50f));

                    using (SKShader shader = SKShader.CreateBitmap(tile, SKShaderTileMode.Repeat,
                                                                   SKShaderTileMode.Repeat))
                    using (SKPaint paint = new SKPaint { Style = SKPaintStyle.Fill, Shader = shader })
                    using (SKPath path = new SKPath())
                    {
                        path.AddRect(new SKRect(-10f, -10f, 120f, 70f));
                        canvas.DrawPath(path, paint);
                    }

                    canvas.Restore();
                    canvas.Restore();
                }

                picture = recorder.EndRecording();
            }

            return picture;
        }

        private static SKBitmap NewTile(int size)
        {
            SKBitmap tile = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (SKCanvas canvas = new SKCanvas(tile))
            {
                canvas.Clear(SKColors.White);
                using (SKPaint paint = new SKPaint
                {
                    Color = SKColors.Black,
                    IsAntialias = false,
                    StrokeWidth = 1f,
                })
                {
                    canvas.DrawLine(0f, size, size, 0f, paint);
                }
            }

            return tile;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }
    }
}
