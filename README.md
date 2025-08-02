# 📊 GroupMe Sales Bot

A lightweight C# console bot for tracking daily sales in a GroupMe group chat. Automatically parses messages for dollar amounts, tallies sales by user, and posts a live leaderboard in the chat on command.

---

## 🚀 Features

- 🔍 **Auto-detects sales** from user messages (e.g., `$50`, `50.00`)
- 🧾 **Real-time leaderboard** with medals for top sellers
- 📥 Responds to GroupMe chat commands:
  - `!mysales` — Shows your sales total for the day  
  - `!leaderboard` — Posts the current leaderboard in chat
- 🕒 Polls messages every 10 seconds when in **listen mode**
- 🧠 Intelligent parsing — ignores old messages and updates only when needed

---

## 💻 How It Works

1. **Connects to the GroupMe API** using your bot and user credentials
2. **Parses all messages from today** for valid sales input (e.g. `$50.00`)
3. **Generates a leaderboard** and replies with totals when prompted

---

## 📦 Console Commands

When you launch the bot, you can type the following in the terminal:
> fetch
📬 Manually fetches recent messages, processes sales, and posts the leaderboard.

> listen
🕵️ Listens in real time for !mysales and !leaderboard commands in chat.

> exit
🚪 Exits the program cleanly.
---
## 🧪 Sample Usage
> Chat log:
> 
> Alice: $120 new sale
> 
> Bob: !mysales
> 
> Bot: Bob, you have no sales submitted today.
> 
> Alice: !leaderboard
> 
> Bot:
> 
> Today's Sales Leaderboard:
> 
> 🥇 1. Alice: $120.00
---
## 🔧 Setup
Edit the constants in Program.cs to match your credentials:

> const string UserAccessToken = "your_user_access_token";

> const string UserId = "your_user_id";

> const string BotId = "your_bot_id";

> const string GroupId = "your_group_id";
---
## 📚 Requirements
> .NET 6.0 or later

> Internet access for GroupMe API calls

> GroupMe user access token and bot token
---
## 🧠 Sale Message Rules
For a message to count as a sale, it must:

> ✅ Start with a dollar amount
> 
> ✅ Be in standard currency format ($50, 50.00, etc.)
> 
> ❌ Not include the amount later in the message (e.g. "I made $50 today" is ignored)
---

## 💡 Developer Notes
Uses async HttpClient to fetch and post data

Leaderboard resets every day (based on UTC)

Automatically fetches more messages if necessary

Emojis used for top 3 leaderboard ranks
