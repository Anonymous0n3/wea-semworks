using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using WebApplication1.Models;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Service
{
    public class MqttNewsService
    {
        private readonly IMqttClient _client;
        private readonly MqttClientOptions _options;
        private readonly ConcurrentBag<NewsMessage> _messages = new();
        private readonly ILogger<MqttNewsService> _logger;
        private readonly CouchDbService _dbService;

        public MqttNewsService(ILogger<MqttNewsService> logger, CouchDbService dbService)
        {
            _logger = logger;
            _dbService = dbService;

            try
            {
                var factory = new MqttFactory();
                _client = factory.CreateMqttClient();

                var portStr = Environment.GetEnvironmentVariable("MQTT_PORT");
                var port = string.IsNullOrEmpty(portStr) ? 8883 : int.Parse(portStr);
                var server = Environment.GetEnvironmentVariable("MQTT_ADDRESS") ?? "708999c1de2e4feabd0c9e0eaabbf368.s1.eu.hivemq.cloud";
                var username = Environment.GetEnvironmentVariable("MQTT_USERNAME") ?? "group04";
                var password = Environment.GetEnvironmentVariable("MQTT_PASSWORD") ?? "WEA2025-sk04";

                _logger.LogInformation("[MqttNewsService] 🟡 INIT - Server: {Server}, Port: {Port}, User: {User}", server, port, username);

                _options = new MqttClientOptionsBuilder()
                    .WithClientId($"client-{Guid.NewGuid()}")
                    .WithTcpServer(server, port)
                    .WithCredentials(username, password)
                    .WithCleanSession(false)
                    .WithTls() // zapíná TLS
                    .Build();

                // ✅ Handler pro příchozí zprávy
                _client.ApplicationMessageReceivedAsync += async e =>
                {
                    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                    _logger.LogInformation("[MqttNewsService] 📩 Message RECEIVED on topic '{Topic}': {Payload}", e.ApplicationMessage.Topic, payload);

                    try
                    {
                        var msg = JsonSerializer.Deserialize<NewsMessage>(payload);
                        if (msg != null)
                        {
                            // Krok 1: Uložit do databáze
                            var saved = await _dbService.SaveNewsMessageAsync(msg);

                            if (saved)
                            {
                                // Krok 2: Přidat do in-memory cache (ConcurrentBag)
                                _messages.Add(msg);
                                _logger.LogInformation("[MqttNewsService] ✅ Message deserialized, SAVED to DB and stored in cache: {Headline}", msg.Title ?? "(no title)");
                            }
                            else
                            {
                                _logger.LogError("[MqttNewsService] ❌ Failed to save message to DB. Not adding to cache.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("[MqttNewsService] ⚠️ Deserialized message is NULL (payload: {Payload})", payload);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[MqttNewsService] ❌ Error deserializing or saving MQTT payload: {Payload}", payload);
                    }

                    await Task.CompletedTask;
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MqttNewsService] ❌ Error during MQTT initialization");
            }

            _dbService = dbService;
        }

        // NOVÁ METODA: Načtení zpráv z DB do in-memory cache
        public async Task InitializeMessagesAsync()
        {
            _logger.LogInformation("[MqttNewsService] 🔄 Initializing messages from CouchDB...");

            // Vyčištění staré cache pro zamezení duplicit
            while (_messages.TryTake(out _)) { }

            try
            {
                // Volání metody z CouchDbService
                var messagesFromDb = await _dbService.GetAllNewsMessagesAsync();

                foreach (var msg in messagesFromDb)
                {
                    _messages.Add(msg);
                }

                _logger.LogInformation("[MqttNewsService] ✅ Successfully loaded {Count} messages from DB into cache.", _messages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MqttNewsService] ❌ Failed to initialize messages from CouchDB.");
            }
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (_client.IsConnected)
                {
                    _logger.LogInformation("[MqttNewsService] 🔌 Already connected to MQTT broker.");
                    return;
                }

                _logger.LogInformation("[MqttNewsService] 🔌 Connecting to MQTT broker...");
                await _client.ConnectAsync(_options);
                _logger.LogInformation("[MqttNewsService] ✅ Connected successfully!");

                // 1. ZAVOLAT INICIALIZACI (načtení z DB do cache)
                await InitializeMessagesAsync();

                // 2. NOVÉ: ZAVOLAT MAZÁNÍ STARÝCH ZPRÁV
                await _dbService.DeleteOldNewsMessagesAsync();

                // 3. Subscribe na topic s QoS AtMostOnce
                await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                    .WithTopic("NEWS")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .Build());

                _logger.LogInformation("[MqttNewsService] 📡 Subscribed to topic 'NEWS'");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MqttNewsService] ❌ Failed to connect or subscribe to MQTT broker");
            }
        }

        public async Task PublishNewsAsync(NewsMessage news)
        {
            try
            {
                if (!_client.IsConnected)
                    await ConnectAsync();

                var payload = JsonSerializer.Serialize(news);
                _logger.LogInformation("[MqttNewsService] 🚀 Publishing news: {Headline}", news.Title ?? news.Title ?? "(no title)");

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic("NEWS")
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .WithRetainFlag(true)
                    .Build();

                await _client.PublishAsync(message);
                _logger.LogInformation("[MqttNewsService] ✅ Message published successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MqttNewsService] ❌ Error publishing MQTT message");
            }
        }

        public IEnumerable<NewsMessage> GetRecentMessages()
        {
            _logger.LogInformation("[MqttNewsService] 📊 Getting all stored messages from CACHE ({Count} messages)...", _messages.Count);

            if (_messages.IsEmpty)
            {
                _logger.LogInformation("[MqttNewsService] ⚠️ Bag _messages je PRÁZDNÝ! Zkuste zkontrolovat databázi.");
            }
            // Logika řazení zůstává stejná, ale pracuje s daty z cache/DB
            var list = _messages
                .OrderByDescending(x =>
                {
                    if (DateTime.TryParseExact(
                        x.Date,
                        "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var parsedDate))
                    {
                        return parsedDate;
                    }
                    return DateTime.MinValue;
                })
                .ToList();

            _logger.LogInformation("[MqttNewsService] 📦 Returning {Count} messages", list.Count);

            return list;
        }



    }
}
