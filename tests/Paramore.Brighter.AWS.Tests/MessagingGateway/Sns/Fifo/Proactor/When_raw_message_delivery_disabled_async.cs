﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Fifo.Proactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SqsRawMessageDeliveryTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAChannelAsync _channel;
    private readonly RoutingKey _routingKey;

    public SqsRawMessageDeliveryTestsAsync()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var channelName = $"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _routingKey = new RoutingKey($"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45));

        const int bufferSize = 10;

        // Set rawMessageDelivery to false
        _channel = _channelFactory.CreateAsyncChannel(new SqsSubscription<MyCommand>(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: _routingKey,
            bufferSize: bufferSize,
            makeChannels: OnMissingChannel.Create,
            rawMessageDelivery: false,
            sqsType: SnsSqsType.Fifo));

        _messageProducer = new SnsMessageProducer(awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create, SnsAttributes = new SnsAttributes { Type = SnsSqsType.Fifo }
            });
    }

    [Fact]
    public async Task When_raw_message_delivery_disabled_async()
    {
        // Arrange
        var messageGroupId = $"MessageGroupId{Guid.NewGuid():N}";
        var deduplicationId = $"DeduplicationId{Guid.NewGuid():N}";
        var messageHeader = new MessageHeader(
            Guid.NewGuid().ToString(),
            _routingKey,
            MessageType.MT_COMMAND,
            correlationId: Guid.NewGuid().ToString(),
            replyTo: RoutingKey.Empty,
            contentType: "text\\plain",
            partitionKey: messageGroupId) { Bag = { [HeaderNames.DeduplicationId] = deduplicationId } };

        var customHeaderItem = new KeyValuePair<string, object>("custom-header-item", "custom-header-item-value");
        messageHeader.Bag.Add(customHeaderItem.Key, customHeaderItem.Value);

        var messageToSend = new Message(messageHeader, new MessageBody("test content one"));

        // Act
        await _messageProducer.SendAsync(messageToSend);

        var messageReceived = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(10000));

        await _channel.AcknowledgeAsync(messageReceived);

        // Assert
        messageReceived.Id.Should().Be(messageToSend.Id);
        messageReceived.Header.Topic.Should().Be(messageToSend.Header.Topic.ToValidSNSTopicName(true));
        messageReceived.Header.MessageType.Should().Be(messageToSend.Header.MessageType);
        messageReceived.Header.CorrelationId.Should().Be(messageToSend.Header.CorrelationId);
        messageReceived.Header.ReplyTo.Should().Be(messageToSend.Header.ReplyTo);
        messageReceived.Header.ContentType.Should().Be(messageToSend.Header.ContentType);
        messageReceived.Header.Bag.Should().ContainKey(customHeaderItem.Key).And.ContainValue(customHeaderItem.Value);
        messageReceived.Body.Value.Should().Be(messageToSend.Body.Value);
        messageReceived.Header.PartitionKey.Should().Be(messageGroupId);
        messageReceived.Header.Bag.Should().ContainKey(HeaderNames.DeduplicationId);
        messageReceived.Header.Bag[HeaderNames.DeduplicationId].Should().Be(deduplicationId);
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
