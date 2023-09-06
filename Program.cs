using DiscordRPC;
using Newtonsoft.Json;

namespace osu_rpc
{
    internal class Program
    {
        public static DiscordRpcClient? client;

        static void Main(string[] args)
        {
            using (client = new DiscordRpcClient("1148786959167271044"))
            {
                client.OnReady += (sender, e) =>
                {
                    Console.WriteLine($"Received Ready from user {e.User.Username}");
                };

                Thread keyboardInterrupt = new Thread(() =>
                {
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    Environment.Exit(0);
                });

                keyboardInterrupt.Start();

                client.Initialize();
                while (true)
                {
                    client.SetPresence(new RichPresence()
                    {
                        Details = "",
                        State = "Idle",
                        Assets = new Assets()
                        {
                            LargeImageKey = "osu-logo",
                            LargeImageText = "salihefee (rank #45,515)",
                            SmallImageKey = "std-small",
                            SmallImageText = "osu!"
                        }
                    });

                    Thread.Sleep(1000);
                }
            }
        }
    }
}