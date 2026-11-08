using System.Net;
using System.Net.Sockets;

var listener = new TcpListener(IPAddress.Loopback, 5000);
listener.Start();
Console.WriteLine("listening on port 5000");

while (true)
{
    var client = listener.AcceptTcpClient();
    Console.WriteLine("client connected");
    Serve(client);
}

void Serve(TcpClient client)
{
    using var stream = client.GetStream();
    var reader = new StreamReader(stream);
    var writer = new StreamWriter(stream) { AutoFlush = true };
    writer.WriteLine("welcome");
    while (reader.ReadLine() is string line)
        writer.WriteLine($"you said: {line}");
    Console.WriteLine("client left");
    client.Dispose();
}
