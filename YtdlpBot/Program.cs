using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

var botClient = new TelegramBotClient("5028045859:AAFGoKM0LMVNyfBP5ePL5ubzuUYYeHPbvI8", baseUrl: "http://localhost:8081");
using var cts = new CancellationTokenSource();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { } // receive all update types
};
botClient.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Type != UpdateType.Message)
        return;
    // Only process text messages
    if (update.Message!.Type != MessageType.Text)
        return;

    var chatId = update.Message.Chat.Id;
    var messageText = update.Message.Text;

    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");
    if (chatId == 737134612)
    {
        if (messageText.Contains("youtu.be") || messageText.Contains("youtube.com"))
        {
            var videoLink = "https://youtu.be/" + Regex.Match(messageText, @"[A-Za-z0-9_\-]{11}").Value;
            var fileName = DownloadVideo(videoLink, "config_bot_yt.txt");
            var probe = VideoProbe(fileName + ".mp4");
        }
        else
        {
            var videoLink = Regex.Match(messageText, @".*(?=\?)").Value;
            var fileName = DownloadVideo(videoLink, "config_bot.txt");
            var probe = VideoProbe(fileName + ".mp4");

            var file = new Uri(@"R:/" + fileName + ".mp4");
            var thumb = new Uri(@"R:/" + fileName + ".jpg");
            //var sb = new StringBuilder(fileName);
            var caption = fileName;
            var escape_symbols = new List<char> { '*', '_', '{', '}', '[', ']', '(', ')', '#', '+', '-', '.', '!' };
            foreach (var c in escape_symbols)
            {
                caption = caption.Replace(c.ToString(), "\\" + c.ToString());
            }
            if (caption == string.Empty)
            {
                caption = "Источник";
            }
            //var thumb = new Uri("R:/thumb.jpg");
            {
                Message message;
                message = await botClient.SendVideoAsync(
                    chatId: chatId,
                    video: new InputOnlineFile(file),
                    thumb: new InputMedia(thumb.ToString()), // Bruh the same as video
                    caption: $"[{caption}]({videoLink})",
                    parseMode: ParseMode.MarkdownV2,
                    width: probe[0],
                    height: probe[1],
                    duration: probe[2],
                    supportsStreaming: true,
                    cancellationToken: cancellationToken);
            }
            // Echo received message text
            //Message sentMessage = await botClient.SendTextMessageAsync(
            //    chatId: chatId,
            //    text: "You said:\n" + messageText,
            //    cancellationToken: cancellationToken);

            //Message message;
            //using (var stream = System.IO.File.OpenRead("../../../src_docs_voice-nfl_commentary.ogg"))
            //{
            //    message = await botClient.SendVoiceAsync(
            //        chatId: chatId,
            //        voice: stream,
            //        duration: 36,
            //        cancellationToken: cancellationToken);
            //}
        }
    }
}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

string DownloadVideo(string url, string config)
{
    string ARGS = $" --ignore-config --config-location C:/Users/megapro17/AppData/Roaming/yt-dlp/" + config;
    string DIR = @"R:/";
    string fileName;

    Directory.SetCurrentDirectory(DIR);
    using (Process process = new Process())
    {
        process.StartInfo.FileName = "yt-dlp";
        process.StartInfo.Arguments = url + ARGS;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        //process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
        process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;

        process.Start();

        // Synchronously read the standard output of the spawned process.
        StreamReader reader = process.StandardOutput;
        fileName = reader.ReadToEnd().Replace("\n", "");
        process.WaitForExit();
    }
    fileName = Regex.Match(fileName, @".*(?=.mp4)").Value;
    Console.WriteLine(fileName);
    return fileName;
}

List<int> VideoProbe(string file)
{
    string ARGS = $"-show_entries format=duration -show_entries stream=width,height -v quiet -of json \"{file}\"";
    string outJson;
    using (Process process = new Process())
    {
        process.StartInfo.FileName = "ffprobe";
        process.StartInfo.Arguments = ARGS;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        //process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
        process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;

        process.Start();

        // Synchronously read the standard output of the spawned process.
        StreamReader reader = process.StandardOutput;
        outJson = reader.ReadToEnd();
        process.WaitForExit();
    }
    var json = JsonDocument.Parse(outJson);
    int width = json.RootElement.GetProperty("streams")[0].GetProperty("width").GetInt32();
    int height = json.RootElement.GetProperty("streams")[0].GetProperty("height").GetInt32();
    var dur = json.RootElement.GetProperty("format").GetProperty("duration").GetString();
    double value = double.Parse(dur, NumberStyles.Any, CultureInfo.InvariantCulture);
    var duration = (int)value;

    return new List<int> { width, height, duration };
}