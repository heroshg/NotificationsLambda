using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using NotificationsLambda.Models;

namespace NotificationsLambda.Repositories;

public interface INotificationRepository
{
    Task SaveAsync(NotificationRecord record, CancellationToken ct = default);
}

public class NotificationDynamoRepository(
    IAmazonDynamoDB dynamoDb,
    string tableName,
    ILogger<NotificationDynamoRepository> logger) : INotificationRepository
{
    public async Task SaveAsync(NotificationRecord record, CancellationToken ct = default)
    {
        var request = new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["id"]        = new AttributeValue { S = record.Id },
                ["userId"]    = new AttributeValue { S = record.UserId },
                ["eventType"] = new AttributeValue { S = record.EventType },
                ["subject"]   = new AttributeValue { S = record.Subject },
                ["body"]      = new AttributeValue { S = record.Body },
                ["createdAt"] = new AttributeValue { S = record.CreatedAt },
                ["ttl"]       = new AttributeValue { N = record.Ttl.ToString() }
            }
        };

        await dynamoDb.PutItemAsync(request, ct);

        logger.LogInformation(
            "Notification saved to DynamoDB — id={Id} userId={UserId} type={EventType}",
            record.Id, record.UserId, record.EventType);
    }
}
