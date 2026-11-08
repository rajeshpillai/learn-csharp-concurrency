using System.Diagnostics;
using System.Text;

const int Width = 1920;
const int Height = 1080;
const int MaxIterations = 2000;

var image = new byte[Width * Height * 3];

var clock = Stopwatch.StartNew();

var top = new Thread(() => RenderRows(0, Height / 2));
var bottom = new Thread(() => RenderRows(Height / 2, Height));
top.Start();
bottom.Start();
top.Join();
bottom.Join();

Console.WriteLine($"rendered in {clock.ElapsedMilliseconds} ms");
SaveImage("mandelbrot.ppm");

void RenderRows(int firstRow, int endRow)
{
    for (int y = firstRow; y < endRow; y++)
    {
        for (int x = 0; x < Width; x++)
        {
            double cr = -2.5 + x * 3.5 / Width;
            double ci = -1.0 + y * 2.0 / Height;
            int n = Escape(cr, ci);
            Paint(x, y, n);
        }
    }
}

static int Escape(double cr, double ci)
{
    double zr = 0, zi = 0;
    int n = 0;
    while (n < MaxIterations && zr * zr + zi * zi <= 4.0)
    {
        double next = zr * zr - zi * zi + cr;
        zi = 2 * zr * zi + ci;
        zr = next;
        n++;
    }
    return n;
}

void Paint(int x, int y, int n)
{
    int i = (y * Width + x) * 3;
    if (n == MaxIterations)
    {
        image[i] = image[i + 1] = image[i + 2] = 0;
        return;
    }
    double t = Math.Pow((double)n / MaxIterations, 0.3);
    image[i] = (byte)(9 * (1 - t) * t * t * t * 255);
    image[i + 1] = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
    image[i + 2] = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);
}

void SaveImage(string path)
{
    using var file = File.Create(path);
    file.Write(Encoding.ASCII.GetBytes($"P6 {Width} {Height} 255\n"));
    file.Write(image);
    Console.WriteLine($"saved {path}");
}
