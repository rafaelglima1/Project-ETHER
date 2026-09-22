using Ether.Domain.Combat;
using Ether.Infrastructure.Memory;
using Ether.Infrastructure.Random;

namespace Ether.Infrastructure.Tests.Memory;

public sealed class InMemoryAbilityCooldownStoreTests
{
    private static readonly AbilityId Ability = new("warrior.power_strike");
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ability_is_ready_when_never_used()
    {
        var store = new InMemoryAbilityCooldownStore();

        Assert.True(store.IsReady(Guid.NewGuid(), Ability, Now));
    }

    [Fact]
    public void Cooldown_blocks_until_it_expires()
    {
        var store = new InMemoryAbilityCooldownStore();
        var character = Guid.NewGuid();

        store.Record(character, Ability, TimeSpan.FromSeconds(6), Now);

        Assert.False(store.IsReady(character, Ability, Now.AddSeconds(5)));
        Assert.True(store.IsReady(character, Ability, Now.AddSeconds(6)));
        Assert.Equal(TimeSpan.Zero, store.Remaining(character, Ability, Now.AddSeconds(7)));
    }

    [Fact]
    public void Cooldowns_are_per_character()
    {
        var store = new InMemoryAbilityCooldownStore();
        store.Record(Guid.NewGuid(), Ability, TimeSpan.FromSeconds(6), Now);

        Assert.True(store.IsReady(Guid.NewGuid(), Ability, Now));
    }

    [Fact]
    public void Zero_cooldown_never_blocks()
    {
        var store = new InMemoryAbilityCooldownStore();
        var character = Guid.NewGuid();

        store.Record(character, Ability, TimeSpan.Zero, Now);

        Assert.True(store.IsReady(character, Ability, Now));
    }
}

public sealed class InMemoryEntityLockProviderTests
{
    [Fact]
    public async Task Lock_serializes_concurrent_access_to_the_same_entity()
    {
        var provider = new InMemoryEntityLockProvider();
        var entity = Guid.NewGuid();
        var concurrent = 0;
        var maxConcurrent = 0;

        async Task WorkAsync()
        {
            await using var handle = await provider.AcquireAsync([entity], CancellationToken.None);
            var current = Interlocked.Increment(ref concurrent);
            maxConcurrent = Math.Max(maxConcurrent, current);
            await Task.Delay(20);
            Interlocked.Decrement(ref concurrent);
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => WorkAsync()));

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task Different_entities_do_not_block_each_other()
    {
        var provider = new InMemoryEntityLockProvider();

        await using var first = await provider.AcquireAsync([Guid.NewGuid()], CancellationToken.None);
        await using var second = await provider.AcquireAsync([Guid.NewGuid()], CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Fact]
    public async Task Lock_is_released_after_dispose()
    {
        var provider = new InMemoryEntityLockProvider();
        var entity = Guid.NewGuid();

        await using (var handle = await provider.AcquireAsync([entity], CancellationToken.None))
        {
            Assert.NotNull(handle);
        }

        var reacquired = await provider.AcquireAsync([entity], CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));

        await using (reacquired)
        {
            Assert.NotNull(reacquired);
        }
    }
}

public sealed class SharedRandomSourceTests
{
    [Fact]
    public void Produces_values_in_the_unit_interval()
    {
        var source = new SharedRandomSource();

        for (var index = 0; index < 100; index++)
        {
            var value = source.NextUnit();
            Assert.InRange(value, 0d, 1d);
        }
    }
}
