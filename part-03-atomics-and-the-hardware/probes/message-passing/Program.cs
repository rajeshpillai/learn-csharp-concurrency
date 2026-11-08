// Message passing: A writes the data, then the flag. B reads the flag,
// then the data. Can B see the flag set but the old data?
// Plain fields, no volatile: the CPU's rules alone decide.
const int Rounds = 10_000_000;
var s = new Shared();

var a = new Thread(() =>
{
    for (int r = 1; r <= Rounds; r++)
    {
        while (s.Go != r) { }
        s.Data = 42;
        s.Ready = 1;
        s.DoneA = r;
    }
});
var b = new Thread(() =>
{
    for (int r = 1; r <= Rounds; r++)
    {
        while (s.Go != r) { }
        int ready = s.Ready;
        int data = s.Data;
        if (ready == 1 && data == 0) s.Stale++;
        if (ready == 1) s.SawReady++;
        s.DoneB = r;
    }
});
a.Start();
b.Start();

for (int r = 1; r <= Rounds; r++)
{
    s.Data = 0;
    s.Ready = 0;
    s.Go = r;
    while (s.DoneA != r || s.DoneB != r) { }
}
Console.WriteLine($"{Rounds:N0} rounds");
Console.WriteLine($"B saw Ready = 1:            {s.SawReady:N0}");
Console.WriteLine($"B saw Ready = 1, Data = 0:  {s.Stale:N0}");

class Shared
{
    public int Data, Ready, Stale, SawReady;
    public volatile int Go, DoneA, DoneB;
}
