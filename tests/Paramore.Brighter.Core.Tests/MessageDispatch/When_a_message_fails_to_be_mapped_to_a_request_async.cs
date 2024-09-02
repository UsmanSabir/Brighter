﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{

    public class MessagePumpFailingMessageTranslationTestsAsync
    {
        private const string Topic = "MyTopic";
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly Channel _channel;

        public MessagePumpFailingMessageTranslationTestsAsync()
        {
            SpyRequeueCommandProcessor commandProcessor = new();
            var provider = new CommandProcessorProvider(commandProcessor);
            _channel = new Channel(new(ChannelName), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, 1000));
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new FailingEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyFailingMapperEvent, FailingEventMessageMapperAsync>();
             
            _messagePump = new MessagePumpAsync<MyFailingMapperEvent>(provider, messageMapperRegistry, null, new InMemoryRequestContextFactory())
            {
                Channel = _channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3
            };

            var unmappableMessage = new Message(new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_EVENT), new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            _channel.Enqueue(unmappableMessage);
        }

        [Fact]
        public async Task When_A_Message_Fails_To_Be_Mapped_To_A_Request_Should_Ack()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            _channel.Stop(new RoutingKey(Topic));

            await Task.WhenAll(task);

            Assert.Empty(_bus.Stream(_routingKey));
        }
    }
}
