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

        public MqttNewsService(ILogger<MqttNewsService> logger)
        {
            _logger = logger;

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
                            _messages.Add(msg);
                            _logger.LogInformation("[MqttNewsService] ✅ Message deserialized and stored: {Headline}", msg.Title ?? msg.Title ?? "(no title)");
                        }
                        else
                        {
                            _logger.LogWarning("[MqttNewsService] ⚠️ Deserialized message is NULL (payload: {Payload})", payload);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[MqttNewsService] ❌ Error deserializing MQTT payload: {Payload}", payload);
                    }

                    await Task.CompletedTask;
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MqttNewsService] ❌ Error during MQTT initialization");
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

                // ✅ Subscribe na topic s QoS AtMostOnce
                await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                    .WithTopic("NEWS")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .Build());

                _logger.LogInformation("[MqttNewsService] 📡 Subscribed to topic 'NEWS'");

                // Po připojení MQTT klient automaticky dostane všechny retained zprávy
                // a handler je uloží do _messages
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
            _logger.LogInformation("[MqttNewsService] 📊 Getting all stored messages...");

            if (_messages.IsEmpty)
            {
                _logger.LogInformation("[MqttNewsService] ⚠️ Bag _messages je PRÁZDNÝ!");
            }
            else
            {
                // 🔹 Vypíše všechny zprávy v bagu
                foreach (var msg in _messages)
                {
                    _logger.LogInformation(
                        "[MqttNewsService] 🔹 Stored message date: {Date} | Headline: {Headline} | Category: {Category} | Link: {Link}",
                        msg.Date,
                        msg.Title ?? "(no title)",
                        msg.Category ?? "(no category)",
                        msg.Link ?? "(no link)"
                    );
                }
            }

            // Vracíme všechny zprávy seřazené od nejnovějších podle data
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
                    return DateTime.MinValue; // nepřesné datum půjde na konec
                })
                .ToList();

            _logger.LogInformation("[MqttNewsService] 📦 Returning {Count} messages", list.Count);

            return list;
        }



    }
}
