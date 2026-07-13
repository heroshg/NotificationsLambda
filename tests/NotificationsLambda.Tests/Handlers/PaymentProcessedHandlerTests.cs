using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationsLambda.Handlers;
using NotificationsLambda.Models;
using NotificationsLambda.Repositories;

namespace NotificationsLambda.Tests.Handlers;

public class PaymentProcessedHandlerTests
{
    private readonly Mock<INotificationRepository> _repoMock = new();

    private PaymentProcessedHandler BuildSut() =>
        new(_repoMock.Object, NullLogger<PaymentProcessedHandler>.Instance);

    private static PaymentProcessedEvent BuildEvent(string status) => new(
        OrderId:   Guid.NewGuid(),
        UserId:    Guid.NewGuid(),
        UserEmail: "player@example.com",
        GameId:    Guid.NewGuid(),
        GameName:  "Hades",
        Price:     59.90m,
        Status:    status);

    [Fact]
    public async Task ApprovedPayment_HandleAsync_SavesConfirmationNotification()
    {
        var evt = BuildEvent("Approved");

        await BuildSut().HandleAsync(evt);

        _repoMock.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.UserId    == evt.UserId.ToString()    &&
                n.EventType == "PaymentConfirmation"    &&
                n.Subject.Contains("Hades")             &&
                n.Body.Contains("aprovada")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectedPayment_HandleAsync_SavesRejectionNotification()
    {
        var evt = BuildEvent("Rejected");

        await BuildSut().HandleAsync(evt);

        _repoMock.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.UserId    == evt.UserId.ToString()    &&
                n.EventType == "PaymentRejection"       &&
                n.Subject.Contains("Hades")             &&
                n.Body.Contains("recusado")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("approved")]   // case insensitive
    [InlineData("APPROVED")]
    public async Task StatusCaseInsensitive_HandleAsync_RecognizesApproved(string status)
    {
        await BuildSut().HandleAsync(BuildEvent(status));

        _repoMock.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n => n.EventType == "PaymentConfirmation"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ExceptionPropagates()
    {
        _repoMock
            .Setup(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DynamoDB error"));

        var act = async () => await BuildSut().HandleAsync(BuildEvent("Approved"));

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }
}
