using SingleFlight;

namespace SingleFlight.Tests;

public class SingleFlightTests
{
    [Fact]
    public async Task RunAsync_WithSingleCaller_ReturnsFactoryValue()
    {
        var key = Guid.NewGuid().ToString();

        var value = await SingleFlight<int>.RunAsync(key, () => Task.FromResult(42));

        value.Should().Be(42);
    }

    [Fact]
    public async Task RunAsync_WithConcurrentCallersSharingKey_RunsOnceAndSharesValue()
    {
        var key = Guid.NewGuid().ToString();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var calls = 0;

        async Task<int> Factory()
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task;
            return 7;
        }

        var owner = SingleFlight<int>.RunAsync(key, Factory);
        await started.Task;
        var joiners = Enumerable.Range(0, 5).Select(_ => SingleFlight<int>.RunAsync(key, Factory)).ToArray();

        release.SetResult();
        var values = await Task.WhenAll([owner, .. joiners]);

        calls.Should().Be(1);
        values.Should().OnlyContain(v => v == 7);
    }

    [Fact]
    public async Task RunAsync_WithManySimultaneousCallers_RunsFactoryOnce()
    {
        const int callers = 100;
        var key = Guid.NewGuid().ToString();
        var release = new TaskCompletionSource();
        var allReached = new TaskCompletionSource();
        var calls = 0;
        var reached = 0;

        async Task<int> Factory()
        {
            Interlocked.Increment(ref calls);
            await release.Task;
            return 7;
        }

        // Every caller hits RunAsync as concurrently as the scheduler allows. The factory stays blocked
        // until all of them have joined, so the winning call is still in flight for every straggler —
        // making "exactly one execution" deterministic instead of timing-dependent.
        var tasks = Enumerable.Range(0, callers).Select(_ => Task.Run(async () =>
        {
            var flight = SingleFlight<int>.RunAsync(key, Factory);
            if (Interlocked.Increment(ref reached) == callers)
                allReached.TrySetResult();
            return await flight;
        })).ToArray();

        await allReached.Task;
        release.SetResult();
        var values = await Task.WhenAll(tasks);

        calls.Should().Be(1);
        values.Should().HaveCount(callers).And.OnlyContain(v => v == 7);
    }

    [Fact]
    public async Task RunAsync_WithDifferentKeys_RunsFactoryPerKey()
    {
        var calls = 0;

        var a = await SingleFlight<int>.RunAsync(Guid.NewGuid().ToString(), () =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(1);
        });
        var b = await SingleFlight<int>.RunAsync(Guid.NewGuid().ToString(), () =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(2);
        });

        calls.Should().Be(2);
        a.Should().Be(1);
        b.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_AfterCompletion_RunsAgain()
    {
        var key = Guid.NewGuid().ToString();
        var calls = 0;

        var first = await SingleFlight<int>.RunAsync(key, () => Task.FromResult(Interlocked.Increment(ref calls)));
        var second = await SingleFlight<int>.RunAsync(key, () => Task.FromResult(Interlocked.Increment(ref calls)));

        first.Should().Be(1);
        second.Should().Be(2);
        calls.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_WhenFactoryThrows_PropagatesToAllCallersAndReleasesKey()
    {
        var key = Guid.NewGuid().ToString();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        async Task<int> Factory()
        {
            started.TrySetResult();
            await release.Task;
            throw new InvalidOperationException("boom");
        }

        var owner = SingleFlight<int>.RunAsync(key, Factory);
        await started.Task;
        var joiner = SingleFlight<int>.RunAsync(key, Factory);

        release.SetResult();

        await FluentActions.Awaiting(() => owner).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => joiner).Should().ThrowAsync<InvalidOperationException>();

        var recovered = await SingleFlight<int>.RunAsync(key, () => Task.FromResult(99));
        recovered.Should().Be(99);
    }

    [Fact]
    public async Task RunAsync_WithNullKey_ThrowsArgumentNullException() =>
        await FluentActions
            .Awaiting(() => SingleFlight<int>.RunAsync(null!, () => Task.FromResult(1)))
            .Should().ThrowAsync<ArgumentNullException>();

    [Fact]
    public async Task RunAsync_WithNullFactory_ThrowsArgumentNullException() =>
        await FluentActions
            .Awaiting(() => SingleFlight<int>.RunAsync(Guid.NewGuid().ToString(), null!))
            .Should().ThrowAsync<ArgumentNullException>();
}
