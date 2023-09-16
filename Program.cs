using System.Diagnostics;
using System.Text;
using DiscordRPC;
using Newtonsoft.Json;

namespace osu_rpc
{
    internal static class Program
    {
        private static DiscordRpcClient? rpcClient;
        private static readonly HttpClient httpClient = new HttpClient();
        private static dynamic? configData;
        private static dynamic? gosumemoryObjects;
        private static int? lastState;
        private static int? lastTime;
        private static long? start;
        private static Process? gosumemory;
        private static string? gosumemoryResponse;
        private static dynamic? users;
        private static string? userInfoString;
        private static bool multiplaying;

        private static bool IsPaused(dynamic response)
        {
            if (lastTime == Convert.ToInt32(response["menu"]["bm"]["time"].current))
            {
                return true;
            }

            lastTime = response["menu"]["bm"]["time"].current;
            return false;
        }

        private static async Task<string> GetUserInfo()
        {
            var userRequest =
                await httpClient.GetAsync(
                    $"https://osu.ppy.sh/api/get_user?k={configData!.osu_token}&u={configData.osu_id}&type=u");
            users = JsonConvert.DeserializeObject(await userRequest.Content.ReadAsStringAsync())!;

            var user = users[0];

            return $"{user.username} (rank #{Convert.ToInt32(user.pp_rank):n0})";
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            Process.GetProcessesByName("gosumemory")[0].Kill();
        }

        private static async Task Main()
        {
            var gameModes = new[,]
            {
                { "osu!", "std-small" },
                { "osu!taiko", "taiko-small" },
                { "osu!catch", "ctb-small" },
                { "osu!mania", "mania-small" }
            };

            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "config.json")))
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "config.json");
                Thread.Sleep(1000);
                await using var writer = new StreamWriter(jsonPath);
                await writer.WriteAsync(
                    "{\r\n  \"osu_token\": \"YOUR API KEY HERE\",\r\n  \"osu_id\": \"YOUR PROFILE ID HERE\",\r\n  \"gosumemory_path\": \"PATH OF GOSUMEMORY\"\r\n}");
            }
            else
            {
                configData = JsonConvert.DeserializeObject(await File.ReadAllTextAsync(@"config.json", Encoding.UTF8));

                using (rpcClient = new DiscordRpcClient("1148786959167271044"))
                {
                    rpcClient.OnReady += (_, e) =>
                    {
                        Console.WriteLine($"Received Ready from user {e.User.Username}");
                    };

                    rpcClient.Initialize();

                    if (Process.GetProcessesByName("gosumemory").Length == 0)
                    {
                        try
                        {
                            gosumemory = new Process();
                            gosumemory.StartInfo.FileName = Convert.ToString(configData!.gosumemory_path);
                            gosumemory.StartInfo.CreateNoWindow = true;

                            gosumemory.Start();
                        }
                        catch (Exception)
                        {
                            await using var errorLog =
                                new StreamWriter(Path.Combine(AppContext.BaseDirectory, "errorLog.txt"));
                            await errorLog.WriteAsync("Edit your config.json file before launching the executable.");

                            Environment.Exit(0);
                        }

                        Thread.Sleep(5000);
                    }

                    AppDomain.CurrentDomain.ProcessExit += OnProcessExit!;

                    while (true)
                    {
                        try
                        {
                            userInfoString = await GetUserInfo();
                        }
                        catch (Exception e)
                        {
                            await using var errorLog =
                                new StreamWriter(Path.Combine(AppContext.BaseDirectory, "errorLog.txt"));
                            await errorLog.WriteAsync(e.ToString());
                            continue;
                        }

                        try
                        {
                            gosumemoryResponse = await httpClient.GetStringAsync("http://127.0.0.1:24050/json");
                        }
                        catch (Exception e)
                        {
                            await using var errorLog =
                                new StreamWriter(Path.Combine(AppContext.BaseDirectory, "errorLog.txt"));
                            await errorLog.WriteAsync(e.ToString());
                            continue;
                        }


                        gosumemoryObjects = JsonConvert.DeserializeObject(gosumemoryResponse);

                        if (gosumemoryObjects!.error == "osu! is not fully loaded!")
                        {
                            if (!rpcClient.IsDisposed) rpcClient.Dispose();
                            continue;
                        }

                        if (rpcClient.IsDisposed)
                        {
                            rpcClient = new DiscordRpcClient("1148786959167271044");
                            rpcClient.Initialize();
                        }

                        if (gosumemoryObjects["menu"].state != "2") multiplaying = false;
                        switch (Convert.ToInt32(gosumemoryObjects["menu"].state))
                        {
                            case 0:
                                if (!IsPaused(gosumemoryObjects))
                                {
                                    rpcClient.SetPresence(new RichPresence
                                    {
                                        Details =
                                            $"Listening to {gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title}",
                                        State = "Idling in the main menu",
                                        Assets = new Assets
                                        {
                                            LargeImageKey = "osu-logo",
                                            LargeImageText = userInfoString,
                                            SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                            SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                        }
                                    });

                                    lastState = 0;
                                    break;
                                }

                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details = "",
                                    State = "Idling in the main menu",
                                    Assets = new Assets
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 0;
                                break;

                            case 5:
                                if (lastState == 2) start = null;
                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details =
                                        $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                    State = "In song select",
                                    Assets = new Assets
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 5;
                                break;

                            case 2:
                                if (!IsPaused(gosumemoryObjects))
                                {
                                    var now = DateTimeOffset.UtcNow;

                                    start = now.ToUnixTimeMilliseconds();

                                    if (lastState == 12)
                                    {
                                        multiplaying = true;
                                    }

                                    rpcClient.SetPresence(new RichPresence
                                    {
                                        Details =
                                            $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]{(Convert.ToString(gosumemoryObjects["menu"]["mods"].str) != "NM" ? " + " + Convert.ToString(gosumemoryObjects["menu"]["mods"].str) : "")}",
                                        State = !multiplaying
                                            ? $"Current: {gosumemoryObjects["gameplay"]["pp"].current}pp FC: {gosumemoryObjects["gameplay"]["pp"].maxThisPlay}pp"
                                            : "Multiplaying",
                                        Timestamps = new Timestamps
                                        {
                                            EndUnixMilliseconds = Convert.ToUInt64(start +
                                                (long)gosumemoryObjects["menu"]["bm"]["time"].full -
                                                (long)gosumemoryObjects["menu"]["bm"]["time"].current)
                                        },
                                        Assets = new Assets
                                        {
                                            LargeImageKey =
                                                $"https://assets.ppy.sh/beatmaps/{gosumemoryObjects["menu"]["bm"].set}/covers/list@2x.jpg",
                                            LargeImageText = userInfoString,
                                            SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                            SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                        }
                                    });

                                    lastState = 2;
                                    break;
                                }

                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details =
                                        $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]{(Convert.ToString(gosumemoryObjects["menu"]["mods"].str) != "NM" ? " + " + Convert.ToString(gosumemoryObjects["menu"]["mods"].str) : "")}",
                                    State = "Paused",
                                    Assets = new Assets
                                    {
                                        LargeImageKey =
                                            $"https://assets.ppy.sh/beatmaps/{gosumemoryObjects["menu"]["bm"].set}/covers/list@2x.jpg",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = "https://i.imgur.com/281tPC5.png",
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 2;
                                break;

                            case 4:
                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details =
                                        $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                    State = "In editor song select",
                                    Assets = new Assets
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 4;
                                break;

                            case 1:
                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details =
                                        $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                    State = "Editing a beatmap",
                                    Assets = new Assets
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 1;
                                break;

                            case 11:
                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details = "",
                                    State = "Looking for a lobby",
                                    Assets = new Assets
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 11;
                                break;

                            case 12:
                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details =
                                        $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]",
                                    State = "In a multiplayer lobby",
                                    Assets = new Assets
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 12;
                                break;

                            case 7:
                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details =
                                        $"{gosumemoryObjects["menu"]["bm"]["metadata"].artist} - {gosumemoryObjects["menu"]["bm"]["metadata"].title} [{gosumemoryObjects["menu"]["bm"]["metadata"].difficulty}]{(Convert.ToString(gosumemoryObjects["menu"]["mods"].str) != "NM" ? " + " + Convert.ToString(gosumemoryObjects["menu"]["mods"].str) : "")}",
                                    State = "In the results screen",
                                    Assets = new Assets
                                    {
                                        LargeImageKey =
                                            $"https://assets.ppy.sh/beatmaps/{gosumemoryObjects["menu"]["bm"].set}/covers/list@2x.jpg",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 7;
                                break;

                            case 15:
                                rpcClient.SetPresence(new RichPresence
                                {
                                    Details = "",
                                    State = "Browsing osu!direct",
                                    Assets = new Assets
                                    {
                                        LargeImageKey = "osu-logo",
                                        LargeImageText = userInfoString,
                                        SmallImageKey = gameModes[gosumemoryObjects["menu"].gameMode, 1],
                                        SmallImageText = gameModes[gosumemoryObjects["menu"].gameMode, 0],
                                    }
                                });

                                lastState = 15;
                                break;
                        }
                        Thread.Sleep(1000);
                    }
                }
            }
        }
    }
}