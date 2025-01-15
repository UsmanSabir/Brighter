﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SnsReDrivePolicySDlqTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessagePump _messagePump;
    private readonly Message _message;
    private readonly string _dlqChannelName;
    private readonly IAmAChannelAsync _channel;
    private readonly SqsMessageProducer _sender;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly SqsSubscription<MyCommand> _subscription;
    private readonly ChannelFactory _channelFactory;

    public SnsReDrivePolicySDlqTestsAsync()
    {
        const string replyTo = "http:\\queueUrl";
        const string contentType = "text\\plain";
        _dlqChannelName = $"Redrive-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Redrive-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Redrive-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(queueName);

        _subscription = new SqsSubscription<MyCommand>(
            name: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            routingKey: routingKey,
            requeueCount: -1,
            requeueDelay: TimeSpan.FromMilliseconds(50),
            messagePumpType: MessagePumpType.Proactor,
            redrivePolicy: new RedrivePolicy(new ChannelName(_dlqChannelName), 2),
            channelType: ChannelType.PointToPoint
        );

        var myCommand = new MyDeferredCommand { Value = "Hello Redrive" };
        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        _awsConnection = GatewayFactory.CreateFactory();

        _sender = new SqsMessageProducer(
            _awsConnection,
            new SqsPublication
            {
                Topic = routingKey,
                RequestType = typeof(MyDeferredCommand),
                MakeChannels = OnMissingChannel.Create,
                SqsAttributes = new SqsAttributes { Type = SnsSqsType.Fifo } 
            }
        );

        _channelFactory = new ChannelFactory(_awsConnection);
        _channel = _channelFactory.CreateAsyncChannel(_subscription);

        IHandleRequestsAsync<MyDeferredCommand> handler = new MyDeferredCommandHandlerAsync();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<MyDeferredCommand, MyDeferredCommandHandlerAsync>();

        IAmACommandProcessor commandProcessor = new CommandProcessor(
            subscriberRegistry: subscriberRegistry,
            handlerFactory: new QuickHandlerFactoryAsync(() => handler),
            requestContextFactory: new InMemoryRequestContextFactory(),
            policyRegistry: new PolicyRegistry()
        );

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyDeferredCommandMessageMapper()),
            null
        );
        messageMapperRegistry.Register<MyDeferredCommand, MyDeferredCommandMessageMapper>();

        _messagePump = new Proactor<MyDeferredCommand>(commandProcessor, messageMapperRegistry,
            new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), _channel)
        {
            Channel = _channel, 
            TimeOut = TimeSpan.FromMilliseconds(5000), 
            RequeueCount = 3
        };
    }

    public async Task<int> GetDLQCountAsync(string queueName)
    {
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = await sqsClient.GetQueueUrlAsync(queueName);
        var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrlResponse.QueueUrl,
            WaitTimeSeconds = 5,
            MessageSystemAttributeNames = new List<string> { "ApproximateReceiveCount" },
            MessageAttributeNames = new List<string> { "All" }
        });

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new AmazonSQSException(
                $"Failed to GetMessagesAsync for queue {queueName}. Response: {response.HttpStatusCode}");
        }

        return response.Messages.Count;
    }

    [Fact(Skip = "Failing async tests caused by task scheduler issues")]
    public async Task When_throwing_defer_action_respect_redrive_async()
    {
        await _sender.SendAsync(_message);

        var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
        await Task.Delay(5000);

        var quitMessage = MessageFactory.CreateQuitMessage(_subscription.RoutingKey);
        _channel.Enqueue(quitMessage);

        await Task.WhenAll(task);

        await Task.Delay(5000);

        var dlqCount = await GetDLQCountAsync(_dlqChannelName + ".fifo");
        dlqCount.Should().Be(1);
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
