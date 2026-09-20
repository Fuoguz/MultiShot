using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconBuilder
{
    private static Bitmap Render(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float s = size / 256f;
            RectangleF bg = new RectangleF(6*s, 6*s, 244*s, 244*s);
            using (GraphicsPath path = RoundedRect(bg, 52*s))
            using (LinearGradientBrush brush = new LinearGradientBrush(bg,
                Color.FromArgb(46,176,247), Color.FromArgb(2,103,230), 45f))
            {
                g.FillPath(brush, path);
                using (Pen edge = new Pen(Color.FromArgb(70,255,255,255), Math.Max(1f,3*s)))
                    g.DrawPath(edge, path);
            }

            using (Pen p = new Pen(Color.White, Math.Max(2f,11*s)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                DrawFrame(g, p, new RectangleF(43*s,54*s,128*s,117*s), 13*s);
                DrawFrame(g, p, new RectangleF(65*s,76*s,128*s,117*s), 13*s);
            }

            using (Pen dash = new Pen(Color.White, Math.Max(2f,8*s)))
            {
                dash.DashPattern = new float[] { 1.7f, 1.2f };
                dash.DashCap = DashCap.Round;
                RectangleF r = new RectangleF(91*s,107*s,115*s,104*s);
                g.DrawRectangle(dash, r.X, r.Y, r.Width, r.Height);
            }

            using (Pen plus = new Pen(Color.White, Math.Max(3f,11*s)))
            {
                plus.StartCap = LineCap.Round;
                plus.EndCap = LineCap.Round;
                g.DrawLine(plus, 174*s,202*s,236*s,202*s);
                g.DrawLine(plus, 205*s,171*s,205*s,233*s);
            }
        }
        return bmp;
    }

    private static void DrawFrame(Graphics g, Pen pen, RectangleF r, float radius)
    {
        using (GraphicsPath path = RoundedRect(r, radius))
            g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2f;
        GraphicsPath p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right-d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom-d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static byte[] PngForSize(int size)
    {
        using (Bitmap bmp = Render(size))
        using (MemoryStream ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    public static int Main()
    {
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> images = new List<byte[]>();
        foreach (int size in sizes) images.Add(PngForSize(size));

        using (FileStream fs = File.Create("MultiShot.ico"))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                byte dim = size >= 256 ? (byte)0 : (byte)size;
                bw.Write(dim);
                bw.Write(dim);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(images[i].Length);
                bw.Write(offset);
                offset += images[i].Length;
            }

            foreach (byte[] image in images) bw.Write(image);
        }

        Console.WriteLine("Generated MultiShot.ico");
        return 0;
    }
}
