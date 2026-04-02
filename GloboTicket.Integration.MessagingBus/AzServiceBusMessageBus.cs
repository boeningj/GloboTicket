using GloboTicket.Integration.Messages;
//using Microsoft.Azure.ServiceBus;
//using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;
using System.Collections.Concurrent;

namespace GloboTicket.Integration.MessagingBus
{
    public class AzServiceBusMessageBus : IMessageBus
    {
        //private readonly string _connectionString;

        // Resue the client to avoid repeatedly opening AMQP connections.
        private readonly ServiceBusClient _client;

        // Thread-safe sender cache (per message topic) ensuring multiple ASP.NET requests can safely publish messages simultaneously.
        private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

        public AzServiceBusMessageBus(string connectionString)
        {
            //_connectionString = connectionString;
            _client = new ServiceBusClient(connectionString);
        }

        public async Task PublishMessage(IntegrationBaseMessage message, string topicName)
        {
            //await using var client = new ServiceBusClient(_connectionString);
            //ServiceBusSender sender = _client.CreateSender(topicName);
            ServiceBusSender sender = GetSender(topicName);

            var jsonMessage = JsonConvert.SerializeObject(message);
            var serviceBusMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(jsonMessage))
            {
                //Connect the business domain event ID to the transport\Service Bus message ID.
                //If you don't set it here then the ASB will assign its own random GUID.
                //By tying the MessageId to the message.Id (e.g. eventUpdatedMessage.Id set in the
                //EventController's POST method when the event info is updated), you ensure the
                //ASB can perform built-in duplicate detection (if enabled) & that your consumer can
                //use the same ID for de-duplication
                MessageId = message.Id.ToString(),
                //CorrelationId = Guid.NewGuid().ToString()
                CorrelationId = message.Id.ToString() //used for tracing/linking related events                
            };

            await sender.SendMessageAsync(serviceBusMessage);
            Console.WriteLine($"Sent message to {topicName}");
        }

        public async Task PublishMessage(IntegrationBaseMessage message, string topicName, string messageType)
        {
            //await using var client = new ServiceBusClient(_connectionString);
            //ServiceBusSender sender = _client.CreateSender(topicName);
            ServiceBusSender sender = GetSender(topicName);

            var jsonMessage = JsonConvert.SerializeObject(message);
            var serviceBusMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(jsonMessage))
            {                
                MessageId = message.Id.ToString(),                
                CorrelationId = message.Id.ToString() //used for tracing/linking related events                
            };
            
            // Used to keep your subscribers version-aware without having to touch the JSON payload
            serviceBusMessage.ApplicationProperties["MessageType"] = messageType;

            await sender.SendMessageAsync(serviceBusMessage);
            Console.WriteLine($"Sent message to {topicName}");
        }

        private ServiceBusSender GetSender(string topicName)
        {
            return _senders.GetOrAdd(topicName, name => _client.CreateSender(name));
        }
    }
}