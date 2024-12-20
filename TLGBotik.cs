using Accord.WindowsForms;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
//using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Extensions.Polling;
using System.Drawing;
using Accord.IO;

namespace NeuralNetwork1
{
    class TLGBotik
    {
        public TelegramBotClient botik = null;

        private BaseNetwork perseptron = null;
        private MagicEye magicEye = null;

        private string[] classLabels;

        public delegate string TalkToAIML(string phrase, Telegram.Bot.Types.User tlgUsr);
        private TalkToAIML talk;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public TLGBotik(BaseNetwork net, Settings settings, string[] labels, TalkToAIML talkTo)
        {
            // Initialize components
            magicEye = new MagicEye
            {
                settings = settings
            };
            magicEye.settings.processImg = true;
            magicEye.settings.border = 165;
            classLabels = labels;
            perseptron = net;

            // Read bot token
            var key = System.IO.File.ReadAllText("..\\..\\botKey.txt");
            botik = new TelegramBotClient(key);

            // Assign AIML delegate
            talk = new TalkToAIML(talkTo);
        }

        public void StartReceiving()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new UpdateType[] { UpdateType.Message } // Only listen for messages
            };

            Console.WriteLine("Bot started receiving updates...");
            botik.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = update.Message;

            Console.WriteLine(message.Text);
            
            

            if (message.Text == "/start")
            {
                var result = talk("СТАРТ", message.From);
                Console.WriteLine(result.ToString());
                botClient.SendTextMessageAsync(message.Chat.Id, result, cancellationToken: cancellationToken);
            }
            else if (message.Type == MessageType.Photo)
            {
                var fileId = message.Photo[message.Photo.Length-1].FileId; // Get the last (highest resolution) photo
                Telegram.Bot.Types.File fileInfo = botik.GetFileAsync(fileId).Result;
                //var fileInfo = botClient.GetFileAsync(fileId, cancellationToken);
                var stream = new MemoryStream();

                await botClient.DownloadFileAsync(fileInfo.FilePath, stream, cancellationToken);
                var img = System.Drawing.Image.FromStream(stream);


                var filePath = "C:\\Users\\TheBogdichHD\\Desktop\\BotSave\\" + message.From;
                System.IO.Directory.CreateDirectory(filePath);
                var fileName = message.Date.Day + "_" + message.Date.Month + "_" + message.Date.Year + "__"
                    + message.Date.Hour + "_" + message.Date.Minute +"_"+ message.Date.Second + ".png";
                var path = Path.Combine(filePath, fileName);
                Console.WriteLine(path);
                img.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                var bitmap_img = new System.Drawing.Bitmap(img);
                bitmap_img.Save(path);

                if (img.Height > img.Width)
                {
                    var tex = "Можешь пожалуйста перевернуть фотографию ~nya";
                    botClient.SendTextMessageAsync(message.Chat.Id, tex, cancellationToken: cancellationToken);
                    return;
                }

                magicEye.ProcessImage(bitmap_img);

                var sample = new Sample(magicEye.sensors, 7);
                perseptron.Predict(sample);

                var recognizedText = new Regex(@"(?<=\().+(?=\))")
                    .Match(classLabels[sample.recognizedClass])
                    .Groups[0]
                    .Value;

                var result = talk(recognizedText, message.From);
                botClient.SendTextMessageAsync(message.Chat.Id, result, cancellationToken: cancellationToken);
            }
            else if (message.Type == MessageType.Text)
            {
                var result = talk(message.Text, message.From);              
                var filePath = "C:\\Users\\TheBogdichHD\\Desktop\\BotSave\\"+message.From;
                System.IO.Directory.CreateDirectory(filePath);
                var fileName = message.From + ".txt";
                var time = message.Date.ToString();
                //var time = message.Date.TimeOfDay.Hours.ToString() + ":" + message.Date.TimeOfDay.Minutes.ToString() + ":" + message.Date.TimeOfDay.Seconds.ToString();
                var path = Path.Combine(filePath, fileName);

                using (StreamWriter sw = System.IO.File.AppendText(path))
                {
                    sw.WriteLine(time);
                    sw.WriteLine(message.From+":");
                    sw.WriteLine(message.Text);
                    sw.WriteLine("Bot:");
                    sw.WriteLine(result+"\n");
                }

                botClient.SendTextMessageAsync(message.Chat.Id, result, cancellationToken: cancellationToken);
            }
            else
            {
                var result = talk("СТРАННОЕ", message.From);
                botClient.SendTextMessageAsync(message.Chat.Id, result, cancellationToken: cancellationToken);
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"An error occurred: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
