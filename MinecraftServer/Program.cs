
class Program
{
    static void Main(string[] args)
    {
        MinecraftServer server = new MinecraftServer(25565);
        server.Start();

        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        server.Stop();
    }
}
    