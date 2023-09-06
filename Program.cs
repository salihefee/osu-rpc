﻿using DiscordRPC;
using DiscordRPC.Message;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace osu_rpc
{
    internal class Program
    {
        public static DiscordRpcClient? rpcClient;

        public static HttpClient httpClient = new HttpClient();

        public static dynamic? configData;

        public static int? lastState = null;

        public static int? lastTime = null;

        public static long? start = null;

        public static Process? gosumemoryProcess;


        public static bool IsPaused(dynamic response)
        {
            if (lastTime == Convert.ToInt32(response["menu"]["bm"]["time"].current))
            {
                return true;
            }
            else
            {
                lastTime = response["menu"]["bm"]["time"].current;
                return false;
            }
        }

        public static async Task<string> GetUserInfo()
        {
            var userRequest = await httpClient.GetAsync($"https://osu.ppy.sh/api/get_user?k={configData!.osu_token}&u={configData!.osu_id}&type=u");
            dynamic users = JsonConvert.DeserializeObject(await userRequest.Content.ReadAsStringAsync())!;
            var user = users[0];

            return $"{user.username} (rank #{Convert.ToInt32(user.pp_rank):n0})";
        }

        public static void OnProcessExit(object sender, EventArgs e)
        {
            gosumemoryProcess!.Kill();
        }

        static async Task Main(string[] args)
        {
            var gameModes = new string[,]
{
                {"osu!", "std-small"},
                {"osu!taiko", "taiko-small"},
                {"osu!catch", "ctb-small"},
                {"osu!mania", "mania-small"}
};

            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "config.json")))
            {
                configData = JsonConvert.DeserializeObject(File.ReadAllText(@"config.json", Encoding.UTF8));

                using (rpcClient = new DiscordRpcClient("1148786959167271044"))
                {
                    rpcClient.OnReady += (sender, e) =>
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

                    rpcClient.Initialize();

                    if (Process.GetProcessesByName("gosumemory").Length == 0)
                    {
                        try
                        {
                            gosumemoryProcess = Process.Start(Convert.ToString(configData!.gosumemory_path));

                            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit!);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Edit your config.json file before launching the executable.");
                            Environment.Exit(0);
                        }
                        
                        Thread.Sleep(5000);
                    }

                    while (true)
                    {
                        dynamic gosumemoryObjects = JsonConvert.DeserializeObject(await httpClient.GetStringAsync("http://127.0.0.1:24050/json"))!;

                        switch (Convert.ToInt32(gosumemoryObjects["menu"].state))
                        {
                            case 0:
                                if (!IsPaused(gosumemoryObjects))
                                {
                                    rpcClient.SetPresence(new RichPresence()
                                    {
                                        Details = $"Listening to {gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title}",
                                        State = "Idling in main menu",
                                        Assets = new Assets()
                                        {
                                            LargeImageKey = "osu-logo",
                                            LargeImageText = await GetUserInfo(),
                                            SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                            SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                        }
                                    });

                                    lastState = 0;
                                    break;
                                }
                                else
                                {

                                    rpcClient.SetPresence(new RichPresence()
                                    {
                                        Details = "",
                                        State = "Idling in the main menu",
                                        Assets = new Assets()
                                        {
                                            LargeImageKey = "osu-logo",
                                            LargeImageText = await GetUserInfo(),
                                            SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                            SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                        }
                                    });

                                    lastState = 0;
                                    break;
                                }

                            case 5:
                                if (lastState == 2) start = null;
                                rpcClient.SetPresence(new RichPresence()
                                {
                                    Details = $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                    State = "In song select",
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = await GetUserInfo(),
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 5;
                                break;

                            case 2:
                                if (!IsPaused(gosumemoryObjects))
                                {
                                    DateTimeOffset now = DateTimeOffset.UtcNow;

                                    start = now.ToUnixTimeMilliseconds();

                                    rpcClient.SetPresence(new RichPresence()
                                    {
                                        Details = $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]{(Convert.ToString(gosumemoryObjects["menu"]["mods"].str) != "NM" ? " + " + Convert.ToString(gosumemoryObjects["menu"]["mods"].str) : "")}",
                                        State = $"Current: {gosumemoryObjects["gameplay"]["pp"].current}pp FC: {gosumemoryObjects["gameplay"]["pp"].maxThisPlay}pp",
                                        Timestamps = new Timestamps()
                                        {
                                            EndUnixMilliseconds = Convert.ToUInt64(start + (long)gosumemoryObjects["menu"]["bm"]["time"].full - (long)gosumemoryObjects["menu"]["bm"]["time"].current)
                                        },
                                        Assets = new Assets()
                                        {
                                            LargeImageKey = $"https://assets.ppy.sh/beatmaps/{gosumemoryObjects["menu"]["bm"].set}/covers/list@2x.jpg",
                                            LargeImageText = await GetUserInfo(),
                                            SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                            SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                        }
                                    });

                                    lastState = 2;
                                    break;
                                }

                                else
                                {
                                    rpcClient.SetPresence(new RichPresence()
                                    {
                                        Details = $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                        State = "Paused",
                                        Assets = new Assets()
                                        {
                                            LargeImageKey = $"https://assets.ppy.sh/beatmaps/{gosumemoryObjects["menu"]["bm"].set}/covers/list@2x.jpg",
                                            LargeImageText = await GetUserInfo(),
                                            SmallImageKey = "https://i.imgur.com/UHbb178.png",
                                            SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                        }
                                    });

                                    lastState = 2;
                                    break;
                                }

                            case 4:
                                rpcClient.SetPresence(new RichPresence()
                                {
                                    Details = $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                    State = "In editor song select",
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = await GetUserInfo(),
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 4;
                                break;

                            case 1:
                                rpcClient.SetPresence(new RichPresence()
                                {
                                    Details = $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                    State = "Editing a beatmap",
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = await GetUserInfo(),
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 1;
                                break;

                            case 11:
                                rpcClient.SetPresence(new RichPresence()
                                {
                                    Details = "",
                                    State = "Looking for a lobby",
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = await GetUserInfo(),
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 11;
                                break;

                            case 12:
                                rpcClient.SetPresence(new RichPresence()
                                {
                                    Details = $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                    State = "In a multiplayer lobby",
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = await GetUserInfo(),
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 12;
                                break;

                            case 7:
                                rpcClient.SetPresence(new RichPresence()
                                {
                                    Details = $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]{(Convert.ToString(gosumemoryObjects["menu"]["mods"].str) != "NM" ? " + " + Convert.ToString(gosumemoryObjects["menu"]["mods"].str) : "")}",
                                    State = "In the results screen",
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = $"https://assets.ppy.sh/beatmaps/{gosumemoryObjects["menu"]["bm"].set}/covers/list@2x.jpg",
                                        LargeImageText = await GetUserInfo(),
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 7;
                                break;

                            case 15:
                                rpcClient.SetPresence(new RichPresence()
                                {
                                    Details = "",
                                    State = "Browsing osu!direct",
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = await GetUserInfo(),
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 15;
                                break;
                        }
                    }
                }
            }

            else
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "config.json");
                Thread.Sleep(1000);
                using (StreamWriter writer = new StreamWriter(jsonPath))
                {
                    writer.Write("{\r\n  \"osu_token\": \"YOUR API KEY HERE\",\r\n  \"osu_id\": \"YOUR PROFILE ID HERE\",\r\n  \"gosumemory_path\": \"PATH OF GOSUMEMORY\"\r\n}");
                }
            }
        }
    }
}