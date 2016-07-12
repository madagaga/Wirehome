﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using HA4IoT.Contracts.Logging;
using HA4IoT.Contracts.PersonalAgent;
using HA4IoT.Contracts.Services;
using HA4IoT.Networking;
using HttpClient = System.Net.Http.HttpClient;

namespace HA4IoT.ExternalServices.TelegramBot
{
    public class TelegramBotService : ServiceBase
    {
        private const string BaseUri = "https://api.telegram.org/bot";

        private readonly BlockingCollection<TelegramOutboundMessage> _pendingMessages = new BlockingCollection<TelegramOutboundMessage>();
        private int _latestUpdateId;
        
        public event EventHandler<TelegramBotMessageReceivedEventArgs> MessageReceived;

        public string AuthenticationToken { get; set; }

        public HashSet<int> Administrators { get; } = new HashSet<int>();
        public HashSet<int> ChatWhitelist { get; } = new HashSet<int>();
        public bool AllowAllClients { get; set; }

        public void Enable()
        {
            Task.Factory.StartNew(
                async () => await ProcessPendingMessagesAsync(),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Task.Factory.StartNew(
                async () => await WaitForUpdates(),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void EnqueueMessage(TelegramOutboundMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            _pendingMessages.Add(message);
        }

        public void EnqueueMessageForAdministrators(string text, TelegramMessageFormat format = TelegramMessageFormat.HTML)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            foreach (var chatId in Administrators)
            {
                EnqueueMessage(new TelegramOutboundMessage(chatId, text, format));
            }
        }

        private async Task SendMessageAsync(TelegramOutboundMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            using (var httpClient = new HttpClient())
            {
                string uri = $"{BaseUri}{AuthenticationToken}/sendMessage";
                StringContent body = ConvertOutboundMessageToJsonMessage(message);
                HttpResponseMessage response = await httpClient.PostAsync(uri, body);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning(
                        $"Sending Telegram message failed (Message='${message.Text}' StatusCode={response.StatusCode}).");
                }
                else
                {
                    Log.Info($"Sent Telegram message '{message.Text}' to chat {message.ChatId}.");
                }
            }
        }

        private async Task ProcessPendingMessagesAsync()
        {
            while (true)
            {
                try
                {
                    TelegramOutboundMessage message = _pendingMessages.Take();
                    await SendMessageAsync(message);
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Error while processing pending Telegram messages.");
                }
            }
        }

        private async Task WaitForUpdates()
        {
            while (true)
            {
                try
                {
                    await WaitForNextUpdates();
                }
                catch (TaskCanceledException)
                {                    
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "Error while waiting for next Telegram updates.");
                }
            }
        }

        private async Task WaitForNextUpdates()
        {
            using (var httpClient = new HttpClient())
            {
                string uri = $"{BaseUri}{AuthenticationToken}/getUpdates?timeout=60&offset={_latestUpdateId + 1}";
                HttpResponseMessage response = await httpClient.GetAsync(uri);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Failed to fetch new updates of TelegramBot (StatusCode={response.StatusCode}).");
                }

                string body = await response.Content.ReadAsStringAsync();
                ProcessUpdates(body);
            }
        }

        private void ProcessUpdates(string body)
        {
            JsonObject response;
            if (!JsonObject.TryParse(body, out response))
            {
                return;
            }

            if (!response.GetNamedBoolean("ok", false))
            {
                return;
            }

            foreach (var updateItem in response.GetNamedArray("result"))
            {
                JsonObject update = updateItem.GetObject();

                _latestUpdateId = (int)update.GetNamedNumber("update_id");
                ProcessMessage(update.GetNamedObject("message"));
            }
        }

        private void ProcessMessage(JsonObject message)
        {
            TelegramInboundMessage inboundMessage = ConvertJsonMessageToInboundMessage(message);

            if (!AllowAllClients && !ChatWhitelist.Contains(inboundMessage.ChatId))
            {
                EnqueueMessage(inboundMessage.CreateResponse("Not authorized!"));

                EnqueueMessageForAdministrators(
                    $"{Emoji.WarningSign} A none whitelisted client ({inboundMessage.ChatId}) has sent a message: '{inboundMessage.Text}'");
            }
            else
            {
                MessageReceived?.Invoke(this, new TelegramBotMessageReceivedEventArgs(this, inboundMessage));
            }
        }

        private TelegramInboundMessage ConvertJsonMessageToInboundMessage(JsonObject message)
        {
            string text = message.GetNamedString("text");
            DateTime timestamp = UnixTimeStampToDateTime(message.GetNamedNumber("date"));

            JsonObject chat = message.GetNamedObject("chat");
            int chatId = (int)chat.GetNamedNumber("id");

            return new TelegramInboundMessage(timestamp, chatId, text);
        }

        private StringContent ConvertOutboundMessageToJsonMessage(TelegramOutboundMessage message)
        {
            if (message.Text.Length > 4096)
            {
                throw new InvalidOperationException("The Telegram outbound message is too long.");
            }

            var json = new JsonObject();
            json.SetNamedNumber("chat_id", message.ChatId);
            json.SetNamedString("text", message.Text);

            if (message.Format == TelegramMessageFormat.HTML)
            {
                json.SetNamedString("parse_mode", "HTML");
            }

            return new StringContent(json.Stringify(), Encoding.UTF8, "application/json");
        }

        private DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            var buffer = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return buffer.AddSeconds(unixTimeStamp).ToLocalTime();
        }
    }
}
