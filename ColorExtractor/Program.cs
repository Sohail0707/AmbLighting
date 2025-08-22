using System;
using System.Drawing;

using System.Windows.Forms;
using System.Drawing.Imaging;

class ScreenEdgeColorSampler
{
    static void Main()
    {
        // Get screen size
        int screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        int screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

        // Margins to leave from edges
        int marginTop = 50, marginBottom = 50, marginLeft = 20, marginRight = 20;

        // Edge LED configuration
        int topLeds = 40, bottomLeds = 40, leftLeds = 25, rightLeds = 25;
        int totalLeds = topLeds + bottomLeds + leftLeds + rightLeds;

        // Target FPS
        int targetFps = 40;
        int frameTimeMs = 1000 / targetFps;

        // Store previous frame's colors
        Color[] prevColors = new Color[totalLeds];
        bool firstFrame = true;
        while (true)
        {
            var frameStart = DateTime.Now;
            Color[] currColors = new Color[totalLeds];
            using (Bitmap bmp = new Bitmap(screenWidth, screenHeight, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                }

                int ledIndex = 0;
                // Top edge
                int topWidth = screenWidth - marginLeft - marginRight;
                int topHeight = Math.Max(1, screenHeight / 40);
                for (int i = 0; i < topLeds; i++, ledIndex++)
                {
                    Rectangle region = new Rectangle(
                        marginLeft + i * topWidth / topLeds,
                        marginTop,
                        topWidth / topLeds,
                        topHeight
                    );
                    currColors[ledIndex] = GetAverageColor(bmp, region);
                }
                // Right edge
                int rightHeight = screenHeight - marginTop - marginBottom;
                int rightWidth = Math.Max(1, screenWidth / 40);
                for (int i = 0; i < rightLeds; i++, ledIndex++)
                {
                    Rectangle region = new Rectangle(
                        screenWidth - marginRight - rightWidth,
                        marginTop + i * rightHeight / rightLeds,
                        rightWidth,
                        rightHeight / rightLeds
                    );
                    currColors[ledIndex] = GetAverageColor(bmp, region);
                }
                // Bottom edge
                int bottomWidth = screenWidth - marginLeft - marginRight;
                int bottomHeight = Math.Max(1, screenHeight / 40);
                for (int i = 0; i < bottomLeds; i++, ledIndex++)
                {
                    Rectangle region = new Rectangle(
                        marginLeft + (bottomLeds - 1 - i) * bottomWidth / bottomLeds,
                        screenHeight - marginBottom - bottomHeight,
                        bottomWidth / bottomLeds,
                        bottomHeight
                    );
                    currColors[ledIndex] = GetAverageColor(bmp, region);
                }
                // Left edge
                int leftHeight = screenHeight - marginTop - marginBottom;
                int leftWidth = Math.Max(1, screenWidth / 40);
                for (int i = 0; i < leftLeds; i++, ledIndex++)
                {
                    Rectangle region = new Rectangle(
                        marginLeft,
                        marginTop + (leftLeds - 1 - i) * leftHeight / leftLeds,
                        leftWidth,
                        leftHeight / leftLeds
                    );
                    currColors[ledIndex] = GetAverageColor(bmp, region);
                }
            }

            // Only print if any color changed
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
                Console.Clear();
                Console.WriteLine($"Total LEDs: {totalLeds}");
                int ledIndex = 0;
                for (int i = 0; i < topLeds; i++, ledIndex++)
                    Console.WriteLine($"Top {i + 1}: RGB({currColors[ledIndex].R},{currColors[ledIndex].G},{currColors[ledIndex].B})");
                for (int i = 0; i < rightLeds; i++, ledIndex++)
                    Console.WriteLine($"Right {i + 1}: RGB({currColors[ledIndex].R},{currColors[ledIndex].G},{currColors[ledIndex].B})");
                for (int i = 0; i < bottomLeds; i++, ledIndex++)
                    Console.WriteLine($"Bottom {i + 1}: RGB({currColors[ledIndex].R},{currColors[ledIndex].G},{currColors[ledIndex].B})");
                for (int i = 0; i < leftLeds; i++, ledIndex++)
                    Console.WriteLine($"Left {i + 1}: RGB({currColors[ledIndex].R},{currColors[ledIndex].G},{currColors[ledIndex].B})");
            }
            prevColors = currColors;
            firstFrame = false;

            // Wait for next frame
            int elapsed = (int)(DateTime.Now - frameStart).TotalMilliseconds;
            int delay = frameTimeMs - elapsed;
            if (delay > 0)
                System.Threading.Thread.Sleep(delay);
        }
    }

    static Color GetAverageColor(Bitmap bmp, Rectangle region)
    {
        long r = 0, g = 0, b = 0;
        int count = 0;
        for (int x = region.Left; x < region.Right; x++)
        {
            for (int y = region.Top; y < region.Bottom; y++)
            {
                Color pixel = bmp.GetPixel(x, y);
                r += pixel.R;
                g += pixel.G;
                b += pixel.B;
                count++;
            }
        }
        if (count == 0) return Color.Black;
        return Color.FromArgb((int)(r / count), (int)(g / count), (int)(b / count));
    }
}
