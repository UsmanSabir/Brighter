﻿using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class PipelineGlobalInboxNoInboxAttributeAsyncTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private AsyncPipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;


        public PipelineGlobalInboxNoInboxAttributeAsyncTests()
        {
            IAmAnInboxSync inbox = new InMemoryInbox(new FakeTimeProvider());
            
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyNoInboxCommandHandlerAsync>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyNoInboxCommandHandlerAsync>();
            container.AddSingleton(inbox);
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _requestContext = new RequestContext();
            
            InboxConfiguration inboxConfiguration = new();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory, inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_Inbox_Async()
        {
            //act
            _chainOfResponsibility = _chainBuilder.BuildAsync(_requestContext, false);
            
            //assert
            var tracer = TracePipelineAsync(_chainOfResponsibility.First());
            tracer.ToString().Should().NotContain("UseInboxHandlerAsync");

        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }

        private PipelineTracer TracePipelineAsync(IHandleRequestsAsync<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
