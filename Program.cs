using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class SaleEntry
{
    public decimal Amount { get; set; }
    public string UserName { get; set; }
}

class Program
{
    static List<SaleEntry> sales = new();

    const string UserAccessToken = "";
    const string UserId = "";
    const string BotId = "";
    const string GroupId = "";

    static async Task Main()
    {
        Console.WriteLine("Adaptive GroupMe Bot Data Scraper");
        Console.WriteLine("Type 'fetch' to show leaderboard, 'listen' to auto-respond to commands, or 'exit' to quit.");

        while (true)
        {
            Console.Write("> ");
            var command = Console.ReadLine()?.Trim().ToLower();
            if (command == "exit") break;

            if (command == "fetch")
            {
                await ProcessGroupMeCommandsAndSales();
            }
            else if (command == "listen")
            {
                await ListenForCommandsAsync();
            }
            else
            {
                Console.WriteLine("Unknown command. Type 'fetch', 'listen', or 'exit'.");
            }
        }
    }

    static async Task ListenForCommandsAsync()
    {
        Console.WriteLine("Listening for !mysales and !leaderboard commands. Press Ctrl+C to stop.");

        // Get the most recent message ID at the start of listening
        var initialMessages = await FetchGroupMeMessagesAsync(UserAccessToken, GroupId, 1);
        string lastMessageId = initialMessages.FirstOrDefault().Id;

        while (true)
        {
            var messages = await FetchGroupMeMessagesAsync(UserAccessToken, GroupId, 20);

            // Only process messages that are NEW since we started listening
            var newMessages = new List<(string Text, string UserName, long CreatedAt, string Id)>();
            if (lastMessageId != null)
            {
                var idx = messages.FindIndex(m => m.Id == lastMessageId);
                if (idx > 0)
                    newMessages = messages.Take(idx).ToList();
            }
            else
            {
                newMessages = messages;
            }

            if (newMessages.Any())
            {
                // Update sales for today
                sales.Clear();
                var allTodayMessages = await FetchAllTodayMessagesAsync(UserAccessToken, GroupId);
                foreach (var msg in allTodayMessages)
                {
                    if (TryParseSale(msg.Text, msg.UserName, out SaleEntry entry))
                        sales.Add(entry);
                }

                // Process only new commands
                foreach (var msg in newMessages)
                {
                    if (msg.Text.Trim().Equals("!leaderboard", StringComparison.OrdinalIgnoreCase))
                    {
                        var leaderboard = BuildLeaderboardMessage();
                        await SendGroupMeBotMessageAsync(BotId, leaderboard);
                    }
                    else if (msg.Text.Trim().Equals("!mysales", StringComparison.OrdinalIgnoreCase))
                    {
                        var myTotal = sales.Where(s => s.UserName == msg.UserName).Sum(s => s.Amount);
                        var reply = myTotal > 0
                            ? $"{msg.UserName}, your total sales today: ${myTotal:N2}"
                            : $"{msg.UserName}, you have no sales submitted today.";
                        await SendGroupMeBotMessageAsync(BotId, reply);
                    }
                }

                // Update lastMessageId to the most recent message processed
                lastMessageId = messages.First().Id;
            }

            await Task.Delay(10000); // Wait 10 seconds before polling again
        }
    }

    static async Task ProcessGroupMeCommandsAndSales()
    {
        Console.WriteLine("Fetching messages from GroupMe...");
        var messages = await FetchGroupMeMessagesAsync(UserAccessToken, GroupId, 100);

        Console.WriteLine($"Fetched {messages.Count} messages.");

        sales.Clear();
        foreach (var msg in messages)
        {
            if (TryParseSale(msg.Text, msg.UserName, out SaleEntry entry))
            {
                sales.Add(entry);
            }
        }

        // Process commands in the last 10 messages (to avoid spamming)
        foreach (var msg in messages.Take(10))
        {
            if (msg.Text.Trim().Equals("!leaderboard", StringComparison.OrdinalIgnoreCase))
            {
                var leaderboard = BuildLeaderboardMessage();
                await SendGroupMeBotMessageAsync(BotId, leaderboard);
            }
            else if (msg.Text.Trim().Equals("!mysales", StringComparison.OrdinalIgnoreCase))
            {
                var myTotal = sales.Where(s => s.UserName == msg.UserName).Sum(s => s.Amount);
                var reply = myTotal > 0
                    ? $"{msg.UserName}, your total sales today: ${myTotal:N2}"
                    : $"{msg.UserName}, you have no sales submitted today.";
                await SendGroupMeBotMessageAsync(BotId, reply);
            }
        }

        // Always print leaderboard to console and send it once on fetch
        var leaderboardMsg = BuildLeaderboardMessage();
        Console.WriteLine(leaderboardMsg);
        await SendGroupMeBotMessageAsync(BotId, leaderboardMsg);
    }

    static bool TryParseSale(string input, string userName, out SaleEntry entry)
    {
        entry = null;
        // Look for a dollar amount at the start of the message
        var match = Regex.Match(input, @"^\$?([\d,]+(?:\.\d{2})?)");
        if (!match.Success) return false;
        if (!decimal.TryParse(match.Groups[1].Value.Replace(",", ""), out decimal amount)) return false;

        entry = new SaleEntry
        {
            Amount = amount,
            UserName = userName
        };
        return true;
    }

    static string BuildLeaderboardMessage()
    {
        if (!sales.Any())
            return "No sales submitted today.";

        var emojis = new[] { "🥇", "🥈", "🥉" };
        var leaderboard = sales
            .GroupBy(s => s.UserName)
            .Select(g => new { UserName = g.Key, Total = g.Sum(s => s.Amount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var message = "Today's Sales Leaderboard:\n";
        int rank = 1;
        foreach (var entry in leaderboard)
        {
            var emoji = rank <= 3 ? emojis[rank - 1] + " " : "";
            message += $"{emoji}{rank}. {entry.UserName}: ${entry.Total:N2}\n";
            rank++;
        }
        return message.TrimEnd();
    }

    static async Task<List<(string Text, string UserName, long CreatedAt, string Id)>> FetchAllTodayMessagesAsync(string userAccessToken, string groupId)
    {
        var allMessages = new List<(string Text, string UserName, long CreatedAt, string Id)>();
        string beforeId = null;
        var today = DateTimeOffset.UtcNow.Date;

        while (true)
        {
            var batch = await FetchGroupMeMessagesAsync(userAccessToken, groupId, 100, beforeId);
            if (batch.Count == 0)
                break;

            // Only keep messages from today
            var todaysBatch = batch
                .Where(m => DateTimeOffset.FromUnixTimeSeconds(m.CreatedAt).Date == today)
                .ToList();

            allMessages.AddRange(todaysBatch);

            // If the batch contains any messages not from today, stop
            if (todaysBatch.Count < batch.Count)
                break;

            // Prepare for next batch
            beforeId = batch.Last().Id;
        }

        return allMessages;
    }

    // Update FetchGroupMeMessagesAsync to accept beforeId
    static async Task<List<(string Text, string UserName, long CreatedAt, string Id)>> FetchGroupMeMessagesAsync(
        string userAccessToken, string groupId, int limit = 100, string beforeId = null)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-Access-Token", userAccessToken);
        var url = $"https://api.groupme.com/v3/groups/{groupId}/messages?limit={limit}";
        if (!string.IsNullOrEmpty(beforeId))
            url += $"&before_id={beforeId}";

        var httpResponse = await client.GetAsync(url);
        if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
        {
            // 304 Not Modified: return empty list (no new messages)
            return new List<(string, string, long, string)>();
        }
        httpResponse.EnsureSuccessStatusCode();

        var response = await httpResponse.Content.ReadFromJsonAsync<GroupMeResponse>();
        return response?.response?.messages?
            .Where(m => !string.IsNullOrWhiteSpace(m.text))
            .Select(m => (m.text, m.name, m.created_at, m.id))
            .ToList() ?? new List<(string, string, long, string)>();
    }

    static async Task SendGroupMeBotMessageAsync(string botId, string text)
    {
        using var client = new HttpClient();
        var url = "https://api.groupme.com/v3/bots/post";
        var payload = new
        {
            bot_id = botId,
            text = text
        };
        await client.PostAsJsonAsync(url, payload);
    }

    // Helper classes for deserialization
    public class GroupMeResponse
    {
        public GroupMeMessages response { get; set; }
    }
    public class GroupMeMessages
    {
        public List<GroupMeMessage> messages { get; set; }
    }
    public class GroupMeMessage
    {
        public string text { get; set; }
        public string name { get; set; }
        public long created_at { get; set; }
        public string id { get; set; }
    }
}
// This code is a simple console application that interacts with the GroupMe API to fetch messages, parse sales data, and maintain a leaderboard.