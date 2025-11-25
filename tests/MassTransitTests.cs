using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Models;

namespace Tests;

public class MassTransitTests
{
    [Fact]
    public async Task Should_Publish_Message_To_Queue()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddHandler<MyMessage>(async (ConsumeContext<MyMessage> context) =>
                {
                    await Task.CompletedTask;
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var bus = provider.GetRequiredService<IBus>();
            await bus.Publish(new MyMessage 
            { 
                MessageId = Guid.NewGuid(), 
                Content = "Test message" 
            });

            Assert.True(await harness.Published.Any<MyMessage>());
            Assert.True(await harness.Consumed.Any<MyMessage>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Should_Consume_Message_With_Correct_Content()
    {
        var messageId = Guid.NewGuid();
        var messageContent = "Test weather forecast";
        
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<MyConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new MyMessage 
            { 
                MessageId = messageId, 
                Content = messageContent 
            });

            Assert.True(await harness.Published.Any<MyMessage>());
            Assert.True(await harness.Consumed.Any<MyMessage>());
            
            var consumed = harness.Consumed.Select<MyMessage>().FirstOrDefault();
            Assert.NotNull(consumed);
            Assert.Equal(messageId, consumed.Context.Message.MessageId);
            Assert.Equal(messageContent, consumed.Context.Message.Content);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Should_Send_Message_To_Specific_Queue()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var sendEndpointProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();
            var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:my-queue"));
            await endpoint.Send(new MyMessage 
            { 
                MessageId = Guid.NewGuid(), 
                Content = "Test message to specific queue" 
            });

            Assert.True(await harness.Sent.Any<MyMessage>());
        }
        finally
        {
            await harness.Stop();
        }
    }
}

public class MyConsumer : IConsumer<MyMessage>
{
    public Task Consume(ConsumeContext<MyMessage> context)
    {
        Console.WriteLine($"[{context.Message.MessageId}] {context.Message.Content}");
        return Task.CompletedTask;
    }
}

