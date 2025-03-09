namespace ChatApp;

internal class ChatAppMain
{
    private static void Main(string[] args)
    {
        if (args.Length == 0) throw new ApplicationException("You must provide a type!");

        if (args[0] == "server")
        {
            var server = new Server();
            server.Run();
        }
        else
        {
            new Client(args[0]).Run();
        }
    }
}