namespace ChatApp;

internal class ChatAppMain
{
    private static async Task Main(string[] args)
    {
        if (args.Length == 0) throw new ApplicationException("You must provide a type!");

        if (args[0] == "server")
        {
            var server = new Server();
            server.Run();
        }
        else
        {
            var client = new Client(args[0]);
            await client.Run();
        }
    }
}