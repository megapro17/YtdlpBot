using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Ytdlp.Models;

namespace YtdlpBot
{
    public class YtdlpBot
    {
        public static void Main(string[] args)
        {
            //Console.WriteLine("Hello World!");
            var t = new YtdlpBot();
            _ = t.Start();
            Console.ReadLine();
            t.Stop();
        }

        private TelegramBotClient botClient = new TelegramBotClient("", baseUrl: "http://localhost:8081");
        private CancellationTokenSource cts = new CancellationTokenSource();
        private ReceiverOptions receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { }, // receive all update types
            ThrowPendingUpdates = true,
        };
        private List<long> WhiteList = new() { };
        async Task Start()
        {
            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token);
            var me = await botClient.GetMeAsync();
            Console.WriteLine($"Start listening for @{me.Username}");
        }
        void Stop() { cts.Cancel(); }
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
            if (WhiteList.Contains(chatId))
            {
                Message InfoMessage;
                InfoMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    disableNotification: true,
                    text: "Видео получено, начинаю обработку...",
                    cancellationToken: cancellationToken);

                //string videoLink;
                YtdlpModel videoInfo;
                if (!Uri.IsWellFormedUriString(messageText, UriKind.Absolute))
                {
                    _ = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        disableNotification: true,
                        text: "Ссылка в сообщении не обнаружена",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    var config = "config_bot.txt";
                    if (messageText.Contains("youtu.be") || messageText.Contains("youtube.com"))
                    {
                        //videoLink = "https://youtu.be/" + Regex.Match(messageText, @"[A-Za-z0-9_\-]{11}").Value;
                        //videoInfo = DownloadVideo(videoLink, "config_bot_yt.txt");
                        config = "config_bot_yt.txt";
                        //probe = VideoProbe(videoInfo.Filename + ".mp4");

                    }
                    videoInfo = GetVideo(messageText, config);
                    if (videoInfo == null)
                    {
                        _ = await botClient.EditMessageTextAsync(
                            chatId: chatId,
                            messageId: InfoMessage.MessageId,
                            parseMode: ParseMode.Html,
                            text: "Иди нахуй",
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        var info = new StringBuilder(10000);
                        info.AppendLine($"<a href=\"{videoInfo.WebpageUrl}\">{videoInfo.Fulltitle}</a>");
                        info.AppendLine($"<b>Автор:</b> <a href=\"{videoInfo.ChannelUrl}\">{videoInfo.Channel}</a>");

                        info.AppendLine($"<b>Качество:</b> {videoInfo.Format} ({videoInfo.Vcodec}+{videoInfo.Acodec})");

                        if (videoInfo.ViewCount != null)
                        {
                            long ViewCount = (long)videoInfo.ViewCount;
                            long LastDigit = ViewCount % 10;
                            long PreLastDigit = ViewCount % 100 / 10;
                            string end = "ов";

                            if (PreLastDigit != 1)
                            {
                                if (LastDigit == 1)
                                    end = String.Empty;
                                else if (2 <= LastDigit && LastDigit <= 4)
                                    end = "а";
                            }
                            info.Append($"<b>{ViewCount} просмотр{end}</b>");

                            info.Append(" • ");
                        }
                        string date = videoInfo.UploadDate.ToString();
                        if (date != String.Empty)
                        {
                            info.AppendLine(DateTime.ParseExact(date, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)
                                .ToString("<b>dd MMMM yyyy<\\/b>", new CultureInfo("ru-RU")));
                        }
                        if (videoInfo.Categories != null)
                        {
                            string category = string.Join(", ", videoInfo.Categories.ToArray());
                            if (category != String.Empty)
                                info.AppendLine($"<b>Категория:</b> {category}");
                        }

                        if (videoInfo.Tags != null)
                        {
                            string tags = string.Join(", ", videoInfo.Tags.ToArray());
                            if (tags != String.Empty)
                                info.AppendLine($"<b>Теги:</b> {tags}");
                        }

                        string description = Regex.Replace(videoInfo.Description, @"[\r\n]{2,}", "\n");
                        if (description != String.Empty)
                            info.AppendLine($"<b>Описание:</b>\n{description}");

                        Message EditMessage;
                        EditMessage = await botClient.EditMessageTextAsync(
                            chatId: chatId,
                            messageId: InfoMessage.MessageId,
                            parseMode: ParseMode.Html,
                            text: info.ToString(),
                            cancellationToken: cancellationToken);

                        DownloadVideo(messageText, config);
                        var file = new Uri(@"R:/" + videoInfo.Filename);
                        int[] thumb = ProcessThumbnail(@"R:/" + videoInfo.Filename);
                        var thumburi = new Uri(@"R:/" + videoInfo.Filename + ".jpg");
                        //var thumb = Image.FromFile(@"R:/" + videoInfo.Filename + ".jpg");

                        //var sb = new StringBuilder(fileName);
                        //var thumb = new Uri("R:/thumb.jpg");
                        Message VideoMessage;
                        VideoMessage = await botClient.SendVideoAsync(
                            chatId: chatId,
                            video: new InputOnlineFile(file),
                            thumb: new InputMedia(thumburi.ToString()), // Bruh the same as video
                            caption: $"<a href=\"{videoInfo.WebpageUrl}\">{videoInfo.Fulltitle}</a>",
                            parseMode: ParseMode.Html,
                            width: thumb[0],
                            height: thumb[1],
                            duration: videoInfo.Duration,
                            supportsStreaming: true,
                            cancellationToken: cancellationToken);

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
        YtdlpModel GetVideo(string url, string config)
        {
            string DIR = @"R:/";
            string jsonInfo;
            Directory.SetCurrentDirectory(DIR);
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "yt-dlp";
                process.StartInfo.Arguments = $"--ignore-config --config-location C:/Users/megapro17/AppData/Roaming/yt-dlp/{config} --dump-json -- \"{url}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                process.Start();

                StreamReader reader = process.StandardOutput;
                jsonInfo = reader.ReadToEnd();
                process.WaitForExit();
            }
            var js = Newtonsoft.Json.JsonConvert.DeserializeObject<YtdlpModel>(jsonInfo);
            return js;
        }
        void DownloadVideo(string url, string config)
        {
            string DIR = @"R:/";
            Directory.SetCurrentDirectory(DIR);
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "yt-dlp";
                process.StartInfo.Arguments = $"--ignore-config --config-location C:/Users/megapro17/AppData/Roaming/yt-dlp/{config} -- \"{url}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;

                process.Start();

                // Synchronously read the standard output of the spawned process.
                StreamReader reader = process.StandardOutput;
                //fileName = reader.ReadToEnd().Replace("\n", "");
                process.WaitForExit();
            }
        }
        int[] ProcessThumbnail(string name)
        {
            string DIR = @"R:/";
            string info;
            int[] thumbInfo;

            Directory.SetCurrentDirectory(DIR);
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "magick";
                process.StartInfo.Arguments = $"convert -define jpeg:extent=200kb \"{name}.png\" \"{name}.jpg\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                process.Start();
                process.WaitForExit();
            }

            using (Process process = new Process())
            {
                process.StartInfo.FileName = "magick";
                process.StartInfo.Arguments = $"identify -ping -format %w,%h \"{name}.png\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                process.Start();
                StreamReader reader = process.StandardOutput;
                info = reader.ReadToEnd();
                thumbInfo = info.Split(',').Select(Int32.Parse).ToArray();
                process.WaitForExit();
            }
            return thumbInfo;
        }

        void ForwardVideo(string message_id)
        {

        }

        //void Escape(ref string text)
        //{
        //    if (text == string.Empty)
        //    {
        //        text = "Источник";
        //    }
        //    else
        //    {
        //        var escape_symbols = new List<char> { '*', '_', '{', '}', '[', ']', '(', ')', '#', '+', '-', '.', '!' };
        //        foreach (var c in escape_symbols)
        //        {
        //            text = text.Replace(c.ToString(), "\\" + c.ToString());
        //        }
        //    }
        //}
        //List<int> VideoProbe(string file)
        //{
        //    string ARGS = $"-show_entries format=duration -show_entries stream=width,height -v quiet -of json \"{file}\"";
        //    string outJson;
        //    using (Process process = new Process())
        //    {
        //        process.StartInfo.FileName = "ffprobe";
        //        process.StartInfo.Arguments = ARGS;
        //        process.StartInfo.UseShellExecute = false;
        //        process.StartInfo.RedirectStandardOutput = true;
        //        //process.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
        //        process.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;

        //        process.Start();

        //        // Synchronously read the standard output of the spawned process.
        //        StreamReader reader = process.StandardOutput;
        //        outJson = reader.ReadToEnd();
        //        process.WaitForExit();
        //    }
        //    var json = JsonDocument.Parse(outJson);
        //    int width = json.RootElement.GetProperty("streams")[0].GetProperty("width").GetInt32();
        //    int height = json.RootElement.GetProperty("streams")[0].GetProperty("height").GetInt32();
        //    var dur = json.RootElement.GetProperty("format").GetProperty("duration").GetString();
        //    double value = double.Parse(dur, NumberStyles.Any, CultureInfo.InvariantCulture);
        //    var duration = (int)value;

        //    return new List<int> { width, height, duration };
        //}
        //void TelegramSend(string file)
        //{
        //    string videoLink;
        //    YtdlpModel videoInfo;
        //    var probe = new List<int>();
        //    if (messageText.Contains("youtu.be") || messageText.Contains("youtube.com"))
        //    {
        //        videoLink = "https://youtu.be/" + Regex.Match(messageText, @"[A-Za-z0-9_\-]{11}").Value;
        //        videoInfo = DownloadVideo(videoLink, "config_bot_yt.txt");
        //        probe = VideoProbe(videoInfo.Filename + ".mp4");
        //        probe[0] = 1280;
        //        probe[1] = 720;
        //    }
        //    else
        //    {
        //        videoLink = Regex.Match(messageText, @".*(?=\?)|.*").Value;
        //        videoInfo = DownloadVideo(videoLink, "config_bot.txt");
        //        probe = VideoProbe(videoInfo.Filename + ".mp4");
        //    }
        //    var file = new Uri(@"R:/" + videoInfo.Filename + ".mp4");
        //    var thumb = new Uri(@"R:/" + videoInfo.Filename + ".jpg");
        //    //var sb = new StringBuilder(fileName);

        //    var caption = videoInfo.Fulltitle;
        //    var escape_symbols = new List<char> { '*', '_', '{', '}', '[', ']', '(', ')', '#', '+', '-', '.', '!' };
        //    foreach (var c in escape_symbols)
        //    {
        //        caption = caption.Replace(c.ToString(), "\\" + c.ToString());
        //    }
        //    if (caption == string.Empty)
        //    {
        //        caption = "Источник";
        //    }
        //}
    }
}

