using Ether.Domain.Accounts;
using Ether.Domain.Common;
using Ether.Domain.Creatures;
using Ether.Domain.Items;
using Ether.Domain.Quests;
using Ether.Domain.Social;
using Ether.Domain.World;

namespace Ether.Domain.Tests.Identifiers;

public sealed class GuidIdentifierTests
{
    [Fact]
    public void New_produces_non_empty_identifiers()
    {
        Assert.False(AccountId.New().IsEmpty);
        Assert.False(CreatureInstanceId.New().IsEmpty);
        Assert.False(ItemInstanceId.New().IsEmpty);
        Assert.False(QuestId.New().IsEmpty);
        Assert.False(GuildId.New().IsEmpty);
        Assert.False(PartyId.New().IsEmpty);
    }

    [Fact]
    public void Constructing_with_empty_guid_throws_domain_exception()
    {
        Assert.Throws<DomainException>(() => { _ = new AccountId(Guid.Empty); });
        Assert.Throws<DomainException>(() => { _ = new CreatureInstanceId(Guid.Empty); });
        Assert.Throws<DomainException>(() => { _ = new ItemInstanceId(Guid.Empty); });
        Assert.Throws<DomainException>(() => { _ = new QuestId(Guid.Empty); });
        Assert.Throws<DomainException>(() => { _ = new GuildId(Guid.Empty); });
        Assert.Throws<DomainException>(() => { _ = new PartyId(Guid.Empty); });
    }

    [Fact]
    public void Default_identifier_is_empty()
    {
        Assert.True(default(AccountId).IsEmpty);
        Assert.True(AccountId.Empty.IsEmpty);
    }

    [Fact]
    public void Identifiers_are_equal_by_value()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new AccountId(guid), new AccountId(guid));
        Assert.Equal(new CreatureInstanceId(guid), new CreatureInstanceId(guid));
        Assert.Equal(new ItemInstanceId(guid), new ItemInstanceId(guid));
        Assert.Equal(new QuestId(guid), new QuestId(guid));
        Assert.Equal(new GuildId(guid), new GuildId(guid));
        Assert.Equal(new PartyId(guid), new PartyId(guid));

        Assert.NotEqual(new AccountId(guid), new AccountId(Guid.NewGuid()));
    }

    [Fact]
    public void ToString_returns_underlying_guid()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(guid.ToString(), new AccountId(guid).ToString());
    }
}
