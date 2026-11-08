const int Seats = 500;
const int Buyers = 10_000;

int seatsLeft = Seats;
int sold = 0;

for (int buyer = 0; buyer < Buyers; buyer++)
{
    if (TryBuy())
        sold++;
}

Console.WriteLine($"tickets sold: {sold}");
Console.WriteLine($"seats left:   {seatsLeft}");

bool TryBuy()
{
    if (seatsLeft > 0)
    {
        seatsLeft--;
        return true;
    }
    return false;
}
