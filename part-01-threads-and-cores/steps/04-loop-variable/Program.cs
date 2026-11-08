using System.Diagnostics;
using System.Text;

const int Width = 1920;
const int Height = 1080;
const int MaxIterations = 2000;

var image = new byte[Width * Height * 3];

int cpus = Environment.ProcessorCount;
int threadCount = args.Length > 0 ? int.Parse(args[0]) : cpus;
Console.WriteLine($"{threadCount} threads, {cpus} logical CPUs");

var clock = Stopwatch.StartNew();

var threads = new List<Thread>();
for (int k = 0; k < threadCount; k++)
{
    var thread = new Thread(() =>
    {
        for (int y = k; y < Height; y += threadCount)
            RenderRows(y, y + 1);
        Done($"thread {k}");
    });
    threads.Add(thread);
    thread.Start();
}
foreach (var thread in threads)
    thread.Join();

Console.WriteLine($"rendered in {clock.ElapsedMilliseconds} ms");
SaveImage("mandelbrot.ppm");

void Done(string what) =>
    Console.WriteLine($"  {what} done at {clock.ElapsedMilliseconds} ms");

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
