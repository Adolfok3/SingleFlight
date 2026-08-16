using Microsoft.Extensions.DependencyInjection;
using SingleFlight;

namespace SingleFlight.Tests;

public class SingleFlightGroupTests
{
    [Fact]
    public async Task RunAsync_WithSingleCaller_ReturnsFactoryValue()
    {
        var group = new SingleFlightGroup<int>();

        var value = await group.RunAsync("key", () => Task.FromResult(42));

        value.Should().Be(42);
    }

    [Fact]
    public async Task ServiceCollection_CanResolveSingleFlightGroup()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(SingleFlightGroup<>));

        using var provider = services.BuildServiceProvider();
        var group = provider.GetRequiredService<SingleFlightGroup<int>>();

        var value = await group.RunAsync("key", () => Task.FromResult(42));

        value.Should().Be(42);
    }

    [Fact]
    public async Task ServiceCollection_CanResolveSingleFlightGroupInterfaceAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ISingleFlightGroup<>), typeof(SingleFlightGroup<>));

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ISingleFlightGroup<int>>();
        var second = provider.GetRequiredService<ISingleFlightGroup<int>>();
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

        var owner = first.RunAsync("key", Factory);
        await started.Task;
        var joiner = second.RunAsync("key", Factory);

        release.SetResult();
        var values = await Task.WhenAll(owner, joiner);

        first.Should().BeSameAs(second);
        calls.Should().Be(1);
        values.Should().OnlyContain(v => v == 7);
    }

    [Fact]
    public async Task ServiceCollection_CanResolveClosedGenericSingleFlightGroupInterface()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingleFlightGroup<User>, SingleFlightGroup<User>>();

        using var provider = services.BuildServiceProvider();
        var group = provider.GetRequiredService<ISingleFlightGroup<User>>();
        var user = new User("ada");

        var value = await group.RunAsync("user:ada", () => Task.FromResult(user));

        value.Should().BeSameAs(user);
    }

    [Fact]
    public async Task RunAsync_WithConcurrentCallersSharingKey_RunsOnceAndSharesValue()
    {
        var group = new SingleFlightGroup<int>();
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

        var owner = group.RunAsync("key", Factory);
        await started.Task;
        var joiners = Enumerable.Range(0, 5).Select(_ => group.RunAsync("key", Factory)).ToArray();

        release.SetResult();
        var values = await Task.WhenAll([owner, .. joiners]);

        calls.Should().Be(1);
        values.Should().OnlyContain(v => v == 7);
    }

    [Fact]
    public async Task RunAsync_WithSameKeyOnDifferentGroups_DoesNotCoalesce()
    {
        var a = new SingleFlightGroup<int>();
        var b = new SingleFlightGroup<int>();
        var calls = 0;

        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();

        async Task<int> Factory()
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task;
            return 1;
        }

        var first = a.RunAsync("key", Factory);
        await started.Task;
        var second = b.RunAsync("key", Factory);

        release.SetResult();
        await Task.WhenAll(first, second);

        calls.Should().Be(2);
    }

    [Fact]
    public async Task RunDetailedAsync_ForOwner_ReportsNotJoined()
    {
        var group = new SingleFlightGroup<int>();

        var result = await group.RunDetailedAsync("key", () => Task.FromResult(5));

        result.Value.Should().Be(5);
        result.Joined.Should().BeFalse();
    }

    [Fact]
    public async Task RunDetailedAsync_ForJoiner_ReportsJoined()
    {
        var group = new SingleFlightGroup<int>();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        async Task<int> Factory()
        {
            started.TrySetResult();
            await release.Task;
            return 7;
        }

        var owner = group.RunDetailedAsync("key", Factory);
        await started.Task;
        var joiner = group.RunDetailedAsync("key", Factory);

        release.SetResult();
        var ownerResult = await owner;
        var joinerResult = await joiner;

        ownerResult.Joined.Should().BeFalse();
        joinerResult.Joined.Should().BeTrue();
        ownerResult.Value.Should().Be(7);
        joinerResult.Value.Should().Be(7);
    }

    [Fact]
    public async Task RunDetailedAsync_Result_Deconstructs()
    {
        var group = new SingleFlightGroup<int>();

        var (value, joined) = await group.RunDetailedAsync("key", () => Task.FromResult(9));

        value.Should().Be(9);
        joined.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithCustomKeyComparer_CoalescesCaseInsensitively()
    {
        var group = new SingleFlightGroup<int>(StringComparer.OrdinalIgnoreCase);
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var calls = 0;

        async Task<int> Factory()
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task;
            return 1;
        }

        var owner = group.RunAsync("KEY", Factory);
        await started.Task;
        var joiner = group.RunAsync("key", Factory);

        release.SetResult();
        await Task.WhenAll(owner, joiner);

        calls.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithNullComparer_ThrowsArgumentNullException()
    {
        var act = () => new SingleFlightGroup<int>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_WithNullKey_ThrowsArgumentNullException() =>
        await FluentActions
            .Awaiting(() => new SingleFlightGroup<int>().RunAsync(null!, () => Task.FromResult(1)))
            .Should().ThrowAsync<ArgumentNullException>();

    [Fact]
    public async Task RunAsync_WithNullFactory_ThrowsArgumentNullException() =>
        await FluentActions
            .Awaiting(() => new SingleFlightGroup<int>().RunAsync("key", null!))
            .Should().ThrowAsync<ArgumentNullException>();

    [Fact]
    public async Task RunDetailedAsync_WithNullKey_ThrowsArgumentNullException() =>
        await FluentActions
            .Awaiting(() => new SingleFlightGroup<int>().RunDetailedAsync(null!, () => Task.FromResult(1)))
            .Should().ThrowAsync<ArgumentNullException>();

    [Fact]
    public async Task RunDetailedAsync_WithNullFactory_ThrowsArgumentNullException() =>
        await FluentActions
            .Awaiting(() => new SingleFlightGroup<int>().RunDetailedAsync("key", null!))
            .Should().ThrowAsync<ArgumentNullException>();

    private sealed record User(string Name);
}
