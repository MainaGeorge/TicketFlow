using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Common.Behaviours;

namespace TicketFlow.Tests.Application.Common.Behaviours;

public class LoggingBehaviourTests
{
    public sealed record TestRequest;
    public sealed record TestResponse(string Value);

    [Fact]
    public async Task Handle_WhenNextReturnsResponse_ReturnsSameResponseAndCallsNextOnce()
    {
        var response = new TestResponse("LoggingTest");
        var count = 0;

        RequestHandlerDelegate<TestResponse> next = (cancellationToken) =>
        {
            count++;
            return Task.FromResult(response);
        };

        var logger = new Mock<ILogger<LoggingBehaviour<TestRequest, TestResponse>>>();
        var behaviour = new LoggingBehaviour<TestRequest, TestResponse>(logger.Object);

        var result = await behaviour.Handle(new TestRequest(), next, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Same(response, result);

        logger.Verify(
            x => x.Log(
                logLevel: LogLevel.Information,
                eventId: It.IsAny<EventId>(),
                state: It.Is<It.IsAnyType>((state, _) => state!.ToString()!.Contains(nameof(TestRequest))),
                exception: It.IsAny<Exception?>(),
                formatter: It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));

        logger.Verify(
            x => x.Log(
                logLevel: LogLevel.Information,
                eventId: It.IsAny<EventId>(),
                state: It.Is<It.IsAnyType>((state, _) => state!.ToString()!.Contains($"About to start handling {nameof(TestRequest)}")),
                exception: It.IsAny<Exception?>(),
                formatter: It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        logger.Verify(
            x => x.Log(
                logLevel: LogLevel.Information,
                eventId: It.IsAny<EventId>(),
                state: It.Is<It.IsAnyType>((state, _) => state!.ToString()!.Contains($"Finished handling {nameof(TestRequest)}")),
                exception: It.IsAny<Exception?>(),
                formatter: It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNextThrows_PropagatesException()
    {
        var exception = new Exception("Something went wrong");

        RequestHandlerDelegate<TestResponse> next = (cancellationToken) => throw exception;
        var logger = new Mock<ILogger<LoggingBehaviour<TestRequest, TestResponse>>>();
        var behaviour = new LoggingBehaviour<TestRequest, TestResponse>(logger.Object);

        var error = await Assert.ThrowsAsync<Exception>(() => behaviour.Handle(new TestRequest(), next, CancellationToken.None));
        Assert.Same(exception, error);

        logger.Verify(
            x => x.Log(
                logLevel: LogLevel.Information,
                eventId: It.IsAny<EventId>(),
                state: It.Is<It.IsAnyType>((state, _) => state!.ToString()!.Contains(nameof(TestRequest))),
                exception: It.IsAny<Exception?>(),
                formatter: It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(1));

        logger.Verify(
            x => x.Log(
                logLevel: LogLevel.Information,
                eventId: It.IsAny<EventId>(),
                state: It.Is<It.IsAnyType>((state, _) => state!.ToString()!.Contains($"About to start handling {nameof(TestRequest)}")),
                exception: It.IsAny<Exception?>(),
                formatter: It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}


