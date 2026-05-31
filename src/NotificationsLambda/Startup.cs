using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationsLambda.Handlers;
using NotificationsLambda.Repositories;

namespace NotificationsLambda;

public static class Startup
{
    public static IServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(b =>
            b.AddConsole().SetMinimumLevel(LogLevel.Information));

        var region = configuration["DynamoDB:Region"] ?? "us-east-1";
        var tableName = configuration["DynamoDB:TableName"]
            ?? throw new InvalidOperationException("DynamoDB__TableName env var is required.");

        services.AddSingleton<IAmazonDynamoDB>(_ =>
            new AmazonDynamoDBClient(new AmazonDynamoDBConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
            }));

        services.AddTransient<INotificationRepository>(sp =>
            new NotificationDynamoRepository(
                sp.GetRequiredService<IAmazonDynamoDB>(),
                tableName,
                sp.GetRequiredService<ILogger<NotificationDynamoRepository>>()));

        services.AddTransient<UserCreatedHandler>();
        services.AddTransient<PaymentProcessedHandler>();

        return services.BuildServiceProvider();
    }
}
