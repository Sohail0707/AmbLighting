
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Text.Json;
using ColorExtractorApp;


// LED order: Start at top-left corner, go right (top edge), then down (right edge), then left (bottom edge), then up (left edge)
// LED index 0 = top-left, then clockwise
class ScreenEdgeColorSampler
{
    // Config path
    static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    static void Main()
    {
    // Load configuration (or create defaults)
    AppConfig cfg = AppConfig.LoadOrCreate(ConfigPath);

    var primary = Screen.PrimaryScreen;
    if (primary == null)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0) throw new InvalidOperationException("No screens detected.");
        primary = screens[0];
    }
    int screenWidth = primary.Bounds.Width;
    int screenHeight = primary.Bounds.Height;
    int marginTop = cfg.MarginTop, marginBottom = cfg.MarginBottom, marginLeft = cfg.MarginLeft, marginRight = cfg.MarginRight;
    // LED layout: Start at middle of bottom edge, go counterclockwise
    int topLeds = cfg.TopLeds, bottomLeds = cfg.BottomLeds, leftLeds = cfg.LeftLeds, rightLeds = cfg.RightLeds;
    int totalLeds = topLeds + bottomLeds + leftLeds + rightLeds;
    // Offset: number of boxes to shift so LED1 is at box[offset]
    int offset = cfg.Offset;
    // LED direction: true = clockwise, false = counterclockwise
    bool clockwise = cfg.Clockwise;
        // Optional saturation boost (to enhance dull colors)
        bool boostSaturation = cfg.BoostSaturation;
        double boostFactor = cfg.BoostFactor; // 1.0 = no change
        double boostMinValue = cfg.BoostMinValue; // don't boost near-black
    // LED index mapping (counterclockwise):
    // 0 .. bottomLeds-1: Bottom edge (middle to left, then right)
    // bottomLeds .. bottomLeds+leftLeds-1: Left edge (bottom to top)
    // bottomLeds+leftLeds .. bottomLeds+leftLeds+topLeds-1: Top edge (right to left)
    // bottomLeds+leftLeds+topLeds .. totalLeds-1: Right edge (top to bottom)
    int targetFps = cfg.TargetFps;
        int frameTimeMs = 1000 / targetFps;
        Color[] prevColors = new Color[totalLeds];
        bool firstFrame = true;
    using (UdpClient udp = new UdpClient())
        using (Bitmap bmp = new Bitmap(screenWidth, screenHeight, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            DateTime nextLog = DateTime.UtcNow;
            while (true)
            {
                var frameStart = DateTime.Now;
                Color[] currColors = new Color[totalLeds];
                // Capture the screen into our reusable bitmap
                g.CopyFromScreen(0, 0, 0, 0, bmp.Size);

                // Lock once per frame for fast pixel access
                Rectangle lockRect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData data = bmp.LockBits(lockRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int ledIndex = 0;
                    int bottomWidth = screenWidth - marginLeft - marginRight;
                    int bottomHeight = Math.Max(1, screenHeight / 40);
                    int leftHeight = screenHeight - marginTop - marginBottom;
                    int leftWidth = Math.Max(1, screenWidth / 40);
                    int topWidth = screenWidth - marginLeft - marginRight;
                    int topHeight = Math.Max(1, screenHeight / 40);
                    int rightHeight = screenHeight - marginTop - marginBottom;
                    int rightWidth = Math.Max(1, screenWidth / 40);

                    // Canonical sampling order (do not change with direction):
                    // Bottom: left -> right
                    for (int i = 0; i < bottomLeds; i++, ledIndex++)
                    {
                        Rectangle region = new Rectangle(
                            marginLeft + i * bottomWidth / bottomLeds,
                            screenHeight - marginBottom - bottomHeight,
                            bottomWidth / bottomLeds,
                            bottomHeight
                        );
                        var col = SampleRegionColorUnsafe(data, region, cfg.SampleStep, cfg.LowResourceMode);
                        if (boostSaturation) col = BoostSaturation(col, boostFactor, boostMinValue);
                        currColors[ledIndex] = col;
                    }
                    // Right: bottom -> top
                    for (int i = 0; i < rightLeds; i++, ledIndex++)
                    {
                        Rectangle region = new Rectangle(
                            screenWidth - marginRight - rightWidth,
                            screenHeight - marginBottom - (i + 1) * rightHeight / rightLeds,
                            rightWidth,
                            rightHeight / rightLeds
                        );
                        var col = SampleRegionColorUnsafe(data, region, cfg.SampleStep, cfg.LowResourceMode);
                        if (boostSaturation) col = BoostSaturation(col, boostFactor, boostMinValue);
                        currColors[ledIndex] = col;
                    }
                    // Top: right -> left
                    for (int i = topLeds - 1; i >= 0; i--, ledIndex++)
                    {
                        Rectangle region = new Rectangle(
                            marginLeft + i * topWidth / topLeds,
                            marginTop,
                            topWidth / topLeds,
                            topHeight
                        );
                        var col = SampleRegionColorUnsafe(data, region, cfg.SampleStep, cfg.LowResourceMode);
                        if (boostSaturation) col = BoostSaturation(col, boostFactor, boostMinValue);
                        currColors[ledIndex] = col;
                    }
                    // Left: top -> bottom
                    for (int i = 0; i < leftLeds; i++, ledIndex++)
                    {
                        Rectangle region = new Rectangle(
                            marginLeft,
                            marginTop + i * leftHeight / leftLeds,
                            leftWidth,
                            leftHeight / leftLeds
                        );
                        var col = SampleRegionColorUnsafe(data, region, cfg.SampleStep, cfg.LowResourceMode);
                        if (boostSaturation) col = BoostSaturation(col, boostFactor, boostMinValue);
                        currColors[ledIndex] = col;
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                // Only send if any color changed
                bool changed = firstFrame;
                if (!firstFrame)
                {
                    for (int i = 0; i < totalLeds; i++)
                    {
                        if (currColors[i].ToArgb() != prevColors[i].ToArgb())
                        {
                            changed = true;
                            break;
                        }
                    }
                }
                if (changed)
                {
                    // Apply offset and direction mapping
                    int offsetNorm = ((offset % totalLeds) + totalLeds) % totalLeds;
                    Color[] offsetColors = new Color[totalLeds];
                    for (int i = 0; i < totalLeds; i++)
                    {
                        offsetColors[i] = currColors[(i + offsetNorm) % totalLeds];
                    }
                    // If direction is counterclockwise, reverse the array
                    if (!clockwise)
                    {
                        Array.Reverse(offsetColors);
                    }
                    // Format: R1,G1,B1,R2,G2,B2,...
                    StringBuilder sb = new StringBuilder();
                    foreach (var c in offsetColors)
                        sb.AppendFormat("{0},{1},{2},", c.R, c.G, c.B);
                    if (sb.Length > 0) sb.Length--; // Remove trailing comma
                    byte[] dataOut = Encoding.ASCII.GetBytes(sb.ToString());
                    try
                    {
                        udp.Send(dataOut, dataOut.Length, cfg.ESP32_IP, cfg.ESP32_PORT);
                        if (DateTime.UtcNow >= nextLog)
                        {
                            Console.WriteLine($"Sent {totalLeds} colors to {cfg.ESP32_IP}:{cfg.ESP32_PORT} (offset {offset}, direction {(clockwise ? "CW" : "CCW")})");
                            nextLog = DateTime.UtcNow.AddSeconds(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"UDP send error: {ex.Message}");
                    }
                }
                prevColors = currColors;
                firstFrame = false;
                int elapsed = (int)(DateTime.Now - frameStart).TotalMilliseconds;
                int delay = frameTimeMs - elapsed;
                if (delay > 0)
                    System.Threading.Thread.Sleep(delay);
            }
        }
    }

    // Fast pixel sampling via LockBits; requires AllowUnsafeBlocks=true
    unsafe static Color SampleRegionColorUnsafe(BitmapData data, Rectangle region, int sampleStep, bool lowResourceMode)
    {
        int width = data.Width;
        int height = data.Height;
        int stride = data.Stride;
        // Clamp region to bitmap bounds
        int left = Math.Max(0, region.Left);
        int top = Math.Max(0, region.Top);
        int right = Math.Min(width, region.Right);
        int bottom = Math.Min(height, region.Bottom);
        if (left >= right || top >= bottom) return Color.Black;

        byte* basePtr = (byte*)data.Scan0.ToPointer();

    if (lowResourceMode)
        {
            int cx = Math.Max(0, Math.Min(width - 1, left + Math.Max(1, (right - left)) / 2));
            int cy = Math.Max(0, Math.Min(height - 1, top + Math.Max(1, (bottom - top)) / 2));
            byte* p = basePtr + cy * stride + (cx << 2);
            // Format32bppArgb -> B,G,R,A
            byte B = p[0];
            byte G = p[1];
            byte R = p[2];
            return Color.FromArgb(R, G, B);
        }

        long r = 0, g = 0, b = 0;
        int count = 0;
    int stepX = Math.Max(1, sampleStep);
    int stepY = Math.Max(1, sampleStep);
        for (int y = top; y < bottom; y += stepY)
        {
            byte* row = basePtr + y * stride;
            for (int x = left; x < right; x += stepX)
            {
                byte* p = row + (x << 2);
                b += p[0];
                g += p[1];
                r += p[2];
                count++;
            }
        }
        if (count == 0) return Color.Black;
        return Color.FromArgb((int)(r / count), (int)(g / count), (int)(b / count));
    }

    // Boost saturation in HSV, leaving hue/value mostly intact.
    static Color BoostSaturation(Color c, double factor, double minValue)
    {
        // Convert to HSV
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double v = max;
        double delta = max - min;
        if (v <= minValue) return c; // too dark, skip boosting
        double s = (max == 0) ? 0 : delta / max;
        double h;
        if (delta == 0) h = 0;
        else if (max == r) h = 60 * (((g - b) / delta) % 6);
        else if (max == g) h = 60 * (((b - r) / delta) + 2);
        else h = 60 * (((r - g) / delta) + 4);
        if (h < 0) h += 360;

        // Boost S
        s = Math.Max(0.0, Math.Min(1.0, s * factor));

        // Convert back HSV -> RGB
        double C = v * s;
        double X = C * (1 - Math.Abs(((h / 60.0) % 2) - 1));
        double m = v - C;
        double rp = 0, gp = 0, bp = 0;
        if (0 <= h && h < 60) { rp = C; gp = X; bp = 0; }
        else if (60 <= h && h < 120) { rp = X; gp = C; bp = 0; }
        else if (120 <= h && h < 180) { rp = 0; gp = C; bp = X; }
        else if (180 <= h && h < 240) { rp = 0; gp = X; bp = C; }
        else if (240 <= h && h < 300) { rp = X; gp = 0; bp = C; }
        else { rp = C; gp = 0; bp = X; }
        byte R = (byte)Math.Round((rp + m) * 255);
        byte G = (byte)Math.Round((gp + m) * 255);
        byte B = (byte)Math.Round((bp + m) * 255);
        return Color.FromArgb(R, G, B);
    }
}
