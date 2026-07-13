using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationsLambda.Handlers;
using NotificationsLambda.Models;
using NotificationsLambda.Repositories;

namespace NotificationsLambda.Tests;

public class FunctionTests
{
    private const string UserCreatedArn        = "arn:aws:sqs:us-east-1:123456789012:fcg-user-created-events-production";
    private const string PaymentProcessedArn   = "arn:aws:sqs:us-east-1:123456789012:fcg-payment-processed-events-production";
    private const string UnknownArn            = "arn:aws:sqs:us-east-1:123456789012:fcg-unknown-queue";

    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly Mock<INotificationRepository> _repoMock = new();
    private readonly Mock<ILambdaContext>           _ctxMock  = new();

    private Function BuildFunction()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning));
        services.AddTransient<INotificationRepository>(_ => _repoMock.Object);
        services.AddTransient<UserCreatedHandler>();
        services.AddTransient<PaymentProcessedHandler>();
        return new Function(services.BuildServiceProvider());
    }

    private static SQSEvent BuildEvent(string eventSourceArn, string body) => new()
    {
        Records =
        [
            new SQSEvent.SQSMessage
            {
                MessageId    = Guid.NewGuid().ToString(),
                EventSourceArn = eventSourceArn,
                Body         = body
            }
        ]
    };

    [Fact]
    public async Task UserCreatedQueue_FunctionHandler_RoutesToUserCreatedHandlerAndPersists()
    {
        var evt  = new UserCreatedEvent(Guid.NewGuid(), "Ana Lima", "ana@example.com");
        var body = JsonSerializer.Serialize(evt, CamelCase);

        var response = await BuildFunction().FunctionHandler(BuildEvent(UserCreatedArn, body), _ctxMock.Object);

        Assert.Empty(response.BatchItemFailures);
        _repoMock.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.EventType == "WelcomeEmail" &&
                n.UserId    == evt.UserId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Approved", "PaymentConfirmation")]
    [InlineData("Rejected", "PaymentRejection")]
    public async Task PaymentProcessedQueue_FunctionHandler_RoutesToPaymentHandlerAndPersists(
        string status, string expectedEventType)
    {
        var evt = new PaymentProcessedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "player@example.com",
            Guid.NewGuid(), "Hades", 59.90m, status);
        var body = JsonSerializer.Serialize(evt, CamelCase);

        var response = await BuildFunction().FunctionHandler(BuildEvent(PaymentProcessedArn, body), _ctxMock.Object);

        Assert.Empty(response.BatchItemFailures);
        _repoMock.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.EventType == expectedEventType &&
                n.UserId    == evt.UserId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnknownQueue_FunctionHandler_IgnoresMessageAndReturnsNoFailures()
    {
        var response = await BuildFunction().FunctionHandler(
            BuildEvent(UnknownArn, "{}"), _ctxMock.Object);

        Assert.Empty(response.BatchItemFailures);
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepositoryThrows_FunctionHandler_AddsToFailuresAndContinues()
    {
        _repoMock
            .Setup(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DynamoDB unavailable"));

        var evt  = new UserCreatedEvent(Guid.NewGuid(), "Test", "test@example.com");
        var body = JsonSerializer.Serialize(evt, CamelCase);
        var sqsEvent = BuildEvent(UserCreatedArn, body);

        var response = await BuildFunction().FunctionHandler(sqsEvent, _ctxMock.Object);

        Assert.Single(response.BatchItemFailures);
        Assert.Equal(sqsEvent.Records[0].MessageId, response.BatchItemFailures[0].ItemIdentifier);
    }

    [Fact]
    public async Task MultipleMixedMessages_FunctionHandler_ProcessesEachCorrectly()
    {
        var userEvt    = new UserCreatedEvent(Guid.NewGuid(), "User1", "user1@example.com");
        var paymentEvt = new PaymentProcessedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "player@example.com",
            Guid.NewGuid(), "Celeste", 19.99m, "Approved");

        var sqsEvent = new SQSEvent
        {
            Records =
            [
                new SQSEvent.SQSMessage
                {
                    MessageId      = "msg-1",
                    EventSourceArn = UserCreatedArn,
                    Body           = JsonSerializer.Serialize(userEvt, CamelCase)
                },
                new SQSEvent.SQSMessage
                {
                    MessageId      = "msg-2",
                    EventSourceArn = PaymentProcessedArn,
                    Body           = JsonSerializer.Serialize(paymentEvt, CamelCase)
                }
            ]
        };

        var response = await BuildFunction().FunctionHandler(sqsEvent, _ctxMock.Object);

        Assert.Empty(response.BatchItemFailures);
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
