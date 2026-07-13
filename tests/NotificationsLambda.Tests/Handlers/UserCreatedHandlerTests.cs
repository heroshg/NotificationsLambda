using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationsLambda.Handlers;
using NotificationsLambda.Models;
using NotificationsLambda.Repositories;

namespace NotificationsLambda.Tests.Handlers;

public class UserCreatedHandlerTests
{
    private readonly Mock<INotificationRepository> _repoMock = new();

    private UserCreatedHandler BuildSut() =>
        new(_repoMock.Object, NullLogger<UserCreatedHandler>.Instance);

    [Fact]
    public async Task HandleAsync_SavesWelcomeEmailNotificationWithCorrectFields()
    {
        var evt = new UserCreatedEvent(Guid.NewGuid(), "Ana Lima", "ana@example.com");

        await BuildSut().HandleAsync(evt);

        _repoMock.Verify(r => r.SaveAsync(
            It.Is<NotificationRecord>(n =>
                n.UserId    == evt.UserId.ToString() &&
                n.EventType == "WelcomeEmail"        &&
                n.Subject.Contains("Ana Lima")       &&
                n.Body.Contains("ana@example.com")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_ExceptionPropagates()
    {
        _repoMock
            .Setup(r => r.SaveAsync(It.IsAny<NotificationRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DynamoDB error"));

        var evt = new UserCreatedEvent(Guid.NewGuid(), "Test", "test@example.com");
        var act = async () => await BuildSut().HandleAsync(evt);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }
}
