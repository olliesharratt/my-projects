using RaftLite;
using Xunit;

public class ElectionTests
{
    private static void AssertElectionSafety(SimCluster c) =>
        Assert.All(c.LeadersByTerm, kv => Assert.True(kv.Value.Count <= 1, $"two leaders in term {kv.Key}: {string.Join(",", kv.Value)}"));

    [Fact]
    public void ElectsExactlyOneLeader()
    {
        var c = new SimCluster(5, seed: 1);
        c.Run(TimeSpan.FromSeconds(2));
        Assert.Single(c.Leaders);
        Assert.Single(c.Nodes.Values.Select(n => n.Term).Distinct());      // everyone agrees on the term
        AssertElectionSafety(c);
    }

    [Fact]
    public void ConvergesForManyRandomSeeds()                              // different seeds exercise split votes
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var c = new SimCluster(5, seed);
            c.Run(TimeSpan.FromSeconds(3));
            Assert.True(c.Leaders.Count() == 1, $"seed {seed}: {c.Leaders.Count()} leaders");
            AssertElectionSafety(c);
        }
    }

    [Fact]
    public void NewLeaderIsElectedAfterLeaderCrashes()
    {
        var c = new SimCluster(5, seed: 2);
        c.Run(TimeSpan.FromSeconds(2));
        var old = c.Leaders.Single();
        var oldTerm = old.Term;

        c.Down.Add(old.Id);
        c.Run(TimeSpan.FromSeconds(2));
        var next = c.Leaders.Single();
        Assert.NotEqual(old.Id, next.Id);
        Assert.True(next.Term > oldTerm);

        c.Down.Remove(old.Id);                                             // the old leader comes back believing it still leads
        c.Run(TimeSpan.FromSeconds(1));
        Assert.Single(c.Leaders);
        Assert.NotEqual(Role.Leader, c.Nodes[old.Id].Role);                // ...and is told to step down
        AssertElectionSafety(c);
    }

    [Fact]
    public void MinorityPartitionCannotElectAndHealingRestoresASingleLeader()
    {
        var c = new SimCluster(5, seed: 3);
        c.Run(TimeSpan.FromSeconds(2));
        var oldLeader = c.Leaders.Single();
        var oldTerm = oldLeader.Term;
        var buddy = c.Nodes.Keys.First(k => k != oldLeader.Id);
        var minority = new[] { oldLeader.Id, buddy };
        var majority = c.Nodes.Keys.Except(minority).ToList();

        c.Partition(minority, majority);
        c.Run(TimeSpan.FromSeconds(2));

        Assert.Equal(Role.Leader, oldLeader.Role);                         // isolated, so it never learns it was replaced...
        Assert.Contains(c.Leaders, l => majority.Contains(l.Id) && l.Term > oldTerm);   // ...while the majority elects a successor

        c.Heal();
        c.Run(TimeSpan.FromSeconds(1));
        Assert.Single(c.Leaders);
        Assert.Single(c.Nodes.Values.Select(n => n.Term).Distinct());
        AssertElectionSafety(c);
    }

    [Fact]
    public void NoLeaderWithoutAQuorum()
    {
        var c = new SimCluster(5, seed: 4);
        foreach (var id in new[] { "n3", "n4", "n5" }) c.Down.Add(id);     // only 2 of 5 alive
        c.Run(TimeSpan.FromSeconds(3));
        Assert.Empty(c.Leaders);
    }

    [Fact]
    public void VoteIsGrantedOnlyOncePerTerm()
    {
        var sent = new List<(string To, IMessage Msg)>();
        var node = new RaftNode("n1", ["n2", "n3"], new Capture(sent), new Random(1), TimeSpan.Zero);
        node.Receive("n2", new RequestVote(1, "n2"), TimeSpan.Zero);
        node.Receive("n3", new RequestVote(1, "n3"), TimeSpan.Zero);
        var replies = sent.Select(s => (VoteReply)s.Msg).ToList();
        Assert.True(replies[0].Granted);
        Assert.False(replies[1].Granted);
    }

    [Fact]
    public void StaleTermRequestsAreRejected()
    {
        var sent = new List<(string To, IMessage Msg)>();
        var node = new RaftNode("n1", ["n2"], new Capture(sent), new Random(1), TimeSpan.Zero);
        node.Receive("n2", new AppendEntries(5, "n2"), TimeSpan.Zero);     // adopt term 5
        node.Receive("n2", new RequestVote(3, "n2"), TimeSpan.Zero);       // old term
        Assert.False(((VoteReply)sent.Last().Msg).Granted);
        Assert.Equal(5, node.Term);
    }

    private sealed class Capture(List<(string, IMessage)> sent) : IRaftTransport
    {
        public void Send(string to, IMessage m) => sent.Add((to, m));
    }
}

public class CodecTests
{
    [Fact]
    public void RoundTripsEveryMessageType()
    {
        IMessage[] all = [new RequestVote(3, "n1"), new VoteReply(3, true), new AppendEntries(4, "n2"), new AppendReply(4)];
        foreach (var m in all)
        {
            var decoded = Codec.Decode(Codec.Encode("n9", m));
            Assert.NotNull(decoded);
            Assert.Equal("n9", decoded.Value.From);
            Assert.Equal(m, decoded.Value.Message);
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"from\":\"n1\",\"type\":\"Nope\",\"body\":{}}")]
    public void GarbageIsIgnoredNotThrown(string input) => Assert.Null(Codec.Decode(input));
}
