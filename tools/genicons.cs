#:package System.Drawing.Common@8.0.10
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Bildet harmoniq-icon.svg nach: Notensystem + drei gebalkte Achtelnoten + Schlussnote
// (Beethovens 5., "ta-ta-ta-taa"). Vollflächiger Hintergrund (maskable-tauglich),
// Motiv zentriert in der Safe-Zone.
void Make(int size, string path)
{
    using var bmp = new Bitmap(size, size);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;

    // Vollflächiger Tile-Hintergrund
    using (var bg = new SolidBrush(ColorTranslator.FromHtml("#241640")))
        g.FillRectangle(bg, 0, 0, size, size);

    // Bounding-Box des Motivs (statt der halb leeren viewBox) auf ~90% einpassen, zentriert.
    const float bx = 60f, by = 85f, bw = 92f, bh = 56f;   // x 60..152, y 85..141
    float s = size * 0.90f / bw;
    float ox = (size - bw * s) / 2f;
    float oy = (size - bh * s) / 2f;
    float X(float x) => ox + (x - bx) * s;
    float Y(float y) => oy + (y - by) * s;
    float Sc(float v) => Math.Max(1f, v * s);

    var staff = Color.FromArgb(115, ColorTranslator.FromHtml("#6D28D9"));
    var noteFill = ColorTranslator.FromHtml("#DDD6FE");
    var beam = ColorTranslator.FromHtml("#A78BFA");
    var last = ColorTranslator.FromHtml("#A855F7");

    // Notenlinien
    using (var pen = new Pen(staff, Sc(0.8f)))
        foreach (var y in new[] { 85f, 97f, 109f, 121f, 133f })
            g.DrawLine(pen, X(60), Y(y), X(150), Y(y));

    void NoteHead(float cx, float cy, Color color, bool filled)
    {
        var state = g.Save();
        g.TranslateTransform(X(cx), Y(cy));
        g.RotateTransform(-15);
        var rx = Sc(6); var ry = Sc(4.5f);
        var rect = new RectangleF(-rx, -ry, rx * 2, ry * 2);
        if (filled) using (var b = new SolidBrush(color)) g.FillEllipse(b, rect);
        else using (var p = new Pen(color, Sc(2))) g.DrawEllipse(p, rect);
        g.Restore(state);
    }

    // Drei gebalkte Achtelnoten (ta-ta-ta)
    using (var stem = new Pen(noteFill, Sc(2)))
    {
        NoteHead(75, 121, noteFill, true); g.DrawLine(stem, X(81), Y(121), X(81), Y(95));
        NoteHead(97, 121, noteFill, true); g.DrawLine(stem, X(103), Y(121), X(103), Y(95));
        NoteHead(119, 121, noteFill, true); g.DrawLine(stem, X(125), Y(121), X(125), Y(95));
    }
    using (var beamPen = new Pen(beam, Sc(3)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        g.DrawLine(beamPen, X(81), Y(95), X(125), Y(95));

    // Schlussnote (taa) + Vorzeichen-Andeutung
    using (var lp = new Pen(last, Sc(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
    {
        // b-Vorzeichen
        g.DrawLine(lp, X(129), Y(123), X(129), Y(139));
        g.DrawBezier(lp, X(129), Y(129), X(136), Y(131), X(136), Y(134), X(129), Y(138));
        // Note
        NoteHead(145, 133, last, false);
        g.DrawLine(lp, X(151), Y(133), X(151), Y(107));
    }

    bmp.Save(path, ImageFormat.Png);
    Console.WriteLine($"geschrieben: {path} ({size}x{size})");
}

Make(512, args[0]);
Make(192, args[1]);
