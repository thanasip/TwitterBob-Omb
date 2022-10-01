using System.Diagnostics;
using TwitterBobomb.Clients;
using Newtonsoft.Json;
using TwitterBobomb.Models;

namespace TwitterBobomb
{
    public class Program
    {
        private const string API_KEY = "*-snip-*";
        private const string API_KEY_SECRET = "*-snip-*";
        private static User _authedUser = new User();
        private static bool _done = false;

        private const string title = @"
  ______         _ __  __               ____        __          ____            __  
 /_  __/      __(_) /_/ /____  _____   / __ )____  / /_        / __ \____ ___  / /_ 
  / / | | /| / / / __/ __/ _ \/ ___/  / __  / __ \/ __ \______/ / / / __ `__ \/ __ \
 / /  | |/ |/ / / /_/ /_/  __/ /     / /_/ / /_/ / /_/ /_____/ /_/ / / / / / / /_/ /
/_/   |__/|__/_/\__/\__/\___/_/     /_____/\____/_.___/      \____/_/ /_/ /_/_.___/";

        public static async Task Main(string[] args)
        {
            Console.WriteLine($"Welcome to the...{title}\n");
            Console.WriteLine("Opening twitter so you can log in...");
            var client = new TwitterClient(API_KEY, API_KEY_SECRET);
            var reqToken = await client.GetRequestToken();
            var uri = $"https://api.twitter.com/oauth/authorize?oauth_token={reqToken}";
            var psi = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = uri
            };
            Process.Start(psi);

            Console.Write("Please enter the access code you got: ");
            var pinCode = Console.ReadLine() ?? "";
            var tokenResponse = await client.GetAccessToken(reqToken, pinCode);
            var token = tokenResponse.Split('&')[0].Split('=')[1];
            var tokenSecret = tokenResponse.Split('&')[1].Split('=')[1];
            _authedUser = JsonConvert.DeserializeObject<UserDto>(await client.GetAuthedUser(token, tokenSecret))?.Data ?? _authedUser;
            Console.WriteLine($"\nWelcome:\n{_authedUser}\n");

            input:
            Console.Write("WARNING: Proceed to delete all tweets? (Y/N): ");
            var yn = Console.ReadKey();
            Console.WriteLine();

            if (yn.Key == ConsoleKey.Y)
            {
                client.TweetDeletedHandler += (sender, e) =>
                {
                    Console.WriteLine($"Successfully deleted tweet: {e.Id}");
                };

                await Task.Run(async () =>
                {
                    var firstRun = true;
                    while (true)
                    {
                        var tweets = JsonConvert.DeserializeObject<TweetDto>(await client.GetLastFiveTweetsForUser(_authedUser?.Id ?? "", token, tokenSecret))?.Data;
                        if (tweets?.Length != 0 && tweets != null)
                        {
                            foreach (var tweet in tweets)
                            {
                                if (firstRun)
                                {
                                    firstRun = false;
                                } else
                                {
                                    Thread.Sleep(20000);
                                }

                                await client.DeleteTweet(tweet.Id ?? "", token, tokenSecret);
                            }
                        }
                        else
                        {
                            _done = true;
                            break;
                        }
                    }
                });
            }
            else if (yn.Key == ConsoleKey.N)
            {
                Console.WriteLine($"Goodbye, {_authedUser.Name}!");
            } 
            else
            {
                Console.WriteLine($"Invalid selection");
                goto input;
            }

            if (_done) Console.WriteLine($"\nDone! Enjoy your freshly clean timeline {_authedUser.Name}!.");
            Console.Write("Press any key to exit...");
            Console.ReadKey(true);
        }
    }
}
