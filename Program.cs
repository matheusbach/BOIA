using System;
using System.Collections.Generic;
using System.Threading;
using Telegram.Bot;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace BIA_telegram
{
    class Program
    {
        private static readonly TelegramBotClient botClient = new TelegramBotClient(System.IO.File.ReadAllText("telegramTokenAPI").Trim('\r', '\n'));
        static Dictionary<string, List<string>> respostas = new Dictionary<string, List<string>>();
        static string[] nomes = new[] { "Bia", "Bradesco", "@BradescoBiaBot" };

        public static System.Timers.Timer timerSaveFrases = new System.Timers.Timer(5000);
        public static Telegram.Bot.Types.User getMe;

        static async System.Threading.Tasks.Task Main(string[] args)
        {
            Console.WriteLine("Iniciando BIA\n");

            LoadFrasesData();

            botClient.OnMessage += BotClient_OnMessage; ;
            botClient.StartReceiving();

            getMe = await botClient.GetMeAsync().ConfigureAwait(false);
            Console.WriteLine(getMe.FirstName + "started");

            while (Console.ReadLine() != "stop") { }
        }

        private static void BotClient_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                if (e.Message.Text.StartsWith("/novaresposta", StringComparison.OrdinalIgnoreCase))
                {
                    // mensagem no formato => /novaresposta palavrachave1, palavra chave2, palavrachave3, [...], palavrachave 100 : mensagem de resposta
                    string mensagem = e.Message.Text.Replace("/novaresposta", "").Replace("@BradescoBiaBot", "");

                    if (mensagem.Split(':').ToList().Count < 2)
                    {
                        botClient.SendTextMessageAsync(e.Message.Chat.Id, "não macaquear. Mensagem no formato => /novaresposta palavrachave1, palavra chave2, palavrachave3, [...], palavrachave 100 : mensagem de resposta", Telegram.Bot.Types.Enums.ParseMode.Markdown, true, false, e.Message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private ? 0 : e.Message.MessageId);
                    }
                    else
                    {
                        string mensagemResposta = mensagem.Split(':').Last();
                        List<string> palavrasChave = mensagem.Split(':')[0].Split(',').Select(s => s.Trim(' ')).Select(s => Regex.Replace(s, @"(?<=^|,) +| +(?=,|$)", "")).ToList();

                        if (!respostas.ContainsKey(mensagemResposta))
                        {
                            respostas.TryAdd(mensagemResposta, palavrasChave);
                        }

                        botClient.SendTextMessageAsync(e.Message.Chat.Id, "resposta cadastrada", Telegram.Bot.Types.Enums.ParseMode.Markdown, true, false, e.Message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private ? 0 : e.Message.MessageId);

                        SaveFrasesData();
                    }
                }

                else if (e.Message.Text.StartsWith("/removeresposta", StringComparison.OrdinalIgnoreCase))
                {
                    // mensagem no formato => /novaresposta palavrachave1, palavra chave2, palavrachave3, [...], palavrachave 100 : mensagem de resposta
                    string mensagem = e.Message.Text.Replace("/removeresposta", "").Replace("@BradescoBiaBot", "");

                    if (mensagem.Length == 0)
                    {
                        botClient.SendTextMessageAsync(e.Message.Chat.Id, "não macaquear. Mensagem no formato => /removeresposta mensagem de resposta a remover", Telegram.Bot.Types.Enums.ParseMode.Markdown, true, false, e.Message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private ? 0 : e.Message.MessageId);
                    }
                    else
                    {
                        string mensagemResposta = mensagem.Split(':').Last();
                        
                        if (respostas.ContainsKey(mensagemResposta))
                        {
                            respostas.Remove(mensagemResposta);
                        }

                        botClient.SendTextMessageAsync(e.Message.Chat.Id, "resposta removida", Telegram.Bot.Types.Enums.ParseMode.Markdown, true, false, e.Message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private ? 0 : e.Message.MessageId);

                        SaveFrasesData();
                    }
                }

                else if (e.Message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private || nomes.Any(s => e.Message.Text.Contains(s, StringComparison.OrdinalIgnoreCase)) || (e.Message.ReplyToMessage != null && e.Message.ReplyToMessage.From.Id == getMe.Id))
                {
                    List<string> resultados = respostas.Where((KeyValuePair<string, List<string>> resposta) => resposta.Value.Any(s => e.Message.Text.Contains(s, StringComparison.OrdinalIgnoreCase))).Select(p => p.Key).ToList();
                    if (resultados.Any())
                    {
                        botClient.SendTextMessageAsync(e.Message.Chat.Id, resultados[new Random().Next(resultados.Count)], Telegram.Bot.Types.Enums.ParseMode.Markdown, true, false, e.Message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private ? 0 : e.Message.MessageId);
                    }
                    else if (e.Message.ReplyToMessage == null || (e.Message.ReplyToMessage != null && e.Message.ReplyToMessage.From.Id != getMe.Id))
                    {
                        botClient.SendTextMessageAsync(e.Message.Chat.Id, "Poxa, não entendi o que você disse. Tente perguntar de outra forma, pode dar certo.\n\nPara adicionar uma nova resposta use o comando /novaresposta", Telegram.Bot.Types.Enums.ParseMode.Markdown, true, false, e.Message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private ? 0 : e.Message.MessageId);
                    }
                }
            }
        }

        public static void SaveFrasesData(bool now = false)
        {
            if (now) { WriteFrasesData(); } else { timerSaveFrases.Elapsed -= timerSaveFrasesData_Elapsed; timerSaveFrases.Elapsed += timerSaveFrasesData_Elapsed; timerSaveFrases.AutoReset = false; timerSaveFrases.Stop(); timerSaveFrases.Start(); }
        }

        private static void timerSaveFrasesData_Elapsed(object sender, ElapsedEventArgs e)
        {
            WriteFrasesData();
        }

        static bool dontWriteFile = false;
        static string tempSerializedData;
        private static void WriteFrasesData()
        {
            while (dontWriteFile) { }

            if (tempSerializedData != JsonSerializer.Serialize(respostas, new JsonSerializerOptions() { WriteIndented = true }))
            {
                dontWriteFile = true;
                tempSerializedData = JsonSerializer.Serialize(respostas, new JsonSerializerOptions() { WriteIndented = true });
                if (!Directory.Exists(Directory.GetCurrentDirectory() + @"/data/")) { Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"/data/"); }
                File.WriteAllText("mensagens.json", tempSerializedData);
                dontWriteFile = false;
            }
        }

        public static void LoadFrasesData()
        {
            dontWriteFile = true;
            if (File.Exists("mensagens.json"))
            {
                respostas = (Dictionary<string, List<string>>)JsonSerializer.Deserialize(File.ReadAllText("mensagens.json"), respostas.GetType());
            }
            dontWriteFile = false;
        }

    }
}
