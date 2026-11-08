const int Rounds = 1_000_000;
var s = new Shared();

var a = new Thread(() =>
{
    for (int r = 1; r <= Rounds; r++)
    {
        while (s.Go != r) { }
        s.X = 1;
        s.SeenByA = s.Y;
        s.DoneA = r;
    }
});
var b = new Thread(() =>
{
    for (int r = 1; r <= Rounds; r++)
    {
        while (s.Go != r) { }
        s.Y = 1;
        s.SeenByB = s.X;
        s.DoneB = r;
    }
});
a.Start();
b.Start();

int both0 = 0;
for (int r = 1; r <= Rounds; r++)
{
    s.X = 0;
    s.Y = 0;
    s.Go = r;
    while (s.DoneA != r || s.DoneB != r) { }
    if (s.SeenByA == 0 && s.SeenByB == 0)
        both0++;
}
Console.WriteLine($"{Rounds:N0} rounds: both saw 0 in {both0:N0}");

class Shared
{
    public volatile int X, Y;
    public int SeenByA, SeenByB;
    // The round signals. volatile: the JIT must read them every time.
    public volatile int Go, DoneA, DoneB;
}
