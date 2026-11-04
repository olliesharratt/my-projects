namespace RaftLite;

/// <summary>
/// Raft leader election as a pure state machine: no sockets, threads or real clocks. The host feeds it time
/// (<see cref="Tick"/>) and messages (<see cref="Receive"/>) and it emits messages through <see cref="IRaftTransport"/>.
/// That makes it deterministic to test under simulated partitions and crashes. Not thread-safe: drive it from one thread.
/// </summary>
public sealed class RaftNode
{
    private readonly IReadOnlyList<string> _peers;
    private readonly IRaftTransport _net;
    private readonly Random _rng;
    private readonly RaftOptions _opt;
    private readonly HashSet<string> _votes = [];
    private TimeSpan _electionDeadline, _nextHeartbeat;

    public RaftNode(string id, IReadOnlyList<string> peers, IRaftTransport net, Random rng, TimeSpan now, RaftOptions? options = null)
    {
        Id = id; _peers = peers; _net = net; _rng = rng; _opt = options ?? new RaftOptions();
        ResetElectionDeadline(now);
    }

    public string Id { get; }
    public int Term { get; private set; }
    public Role Role { get; private set; } = Role.Follower;
    public string? LeaderId { get; private set; }
    public string? VotedFor { get; private set; }
    public event Action<string>? Log;

    private int Quorum => (_peers.Count + 1) / 2 + 1;                       // strict majority of the whole cluster

    /// <summary>Call frequently (every few ms) with the current time.</summary>
    public void Tick(TimeSpan now)
    {
        if (Role == Role.Leader)
        {
            if (now >= _nextHeartbeat) { Broadcast(new AppendEntries(Term, Id)); _nextHeartbeat = now + TimeSpan.FromMilliseconds(_opt.HeartbeatMs); }
        }
        else if (now >= _electionDeadline) StartElection(now);              // no heartbeat or vote granted in time
    }

    public void Receive(string from, IMessage message, TimeSpan now)
    {
        switch (message)
        {
            case RequestVote rv:
                if (rv.Term > Term) StepDown(rv.Term, now);
                // One vote per term, first come first served. (Full Raft also requires the candidate's log to be at
                // least as up to date as ours; this version has no log, so that check is trivially true.)
                var grant = rv.Term == Term && (VotedFor is null || VotedFor == rv.CandidateId);
                if (grant) { VotedFor = rv.CandidateId; ResetElectionDeadline(now); Log?.Invoke($"[{Id}] term {Term}: voted for {rv.CandidateId}"); }
                _net.Send(from, new VoteReply(Term, grant));
                break;

            case VoteReply vr:
                if (vr.Term > Term) { StepDown(vr.Term, now); break; }
                if (Role == Role.Candidate && vr.Term == Term && vr.Granted)
                {
                    _votes.Add(from);
                    if (_votes.Count >= Quorum) BecomeLeader(now);
                }
                break;

            case AppendEntries ae:
                if (ae.Term < Term) { _net.Send(from, new AppendReply(Term)); break; }   // stale leader: tell it the new term
                if (ae.Term > Term) StepDown(ae.Term, now);
                Role = Role.Follower;                                       // a candidate that sees a leader for its own term yields
                LeaderId = ae.LeaderId;
                ResetElectionDeadline(now);                                 // valid heartbeat: leader is alive
                _net.Send(from, new AppendReply(Term));
                break;

            case AppendReply ar:
                if (ar.Term > Term) StepDown(ar.Term, now);
                break;
        }
    }

    private void StartElection(TimeSpan now)
    {
        Term++;
        Role = Role.Candidate;
        VotedFor = Id;
        LeaderId = null;
        _votes.Clear(); _votes.Add(Id);                                     // vote for myself
        ResetElectionDeadline(now);                                         // fresh random timeout: this is what breaks split votes
        Log?.Invoke($"[{Id}] term {Term}: election timeout, requesting votes");
        if (_votes.Count >= Quorum) BecomeLeader(now);                      // single-node cluster
        else Broadcast(new RequestVote(Term, Id));
    }

    private void BecomeLeader(TimeSpan now)
    {
        Role = Role.Leader;
        LeaderId = Id;
        Log?.Invoke($"[{Id}] term {Term}: became LEADER with {_votes.Count} votes");
        Broadcast(new AppendEntries(Term, Id));                             // assert authority immediately
        _nextHeartbeat = now + TimeSpan.FromMilliseconds(_opt.HeartbeatMs);
    }

    /// <summary>Saw a higher term: adopt it, forget our vote and stop being leader/candidate.</summary>
    private void StepDown(int newTerm, TimeSpan now)
    {
        if (Role == Role.Leader) { Log?.Invoke($"[{Id}] saw term {newTerm} > {Term}: stepping down"); ResetElectionDeadline(now); }
        Term = newTerm;
        Role = Role.Follower;
        VotedFor = null;
        LeaderId = null;
        _votes.Clear();
    }

    private void ResetElectionDeadline(TimeSpan now) =>
        _electionDeadline = now + TimeSpan.FromMilliseconds(_rng.Next(_opt.ElectionMinMs, _opt.ElectionMaxMs));

    private void Broadcast(IMessage m) { foreach (var p in _peers) _net.Send(p, m); }
}
