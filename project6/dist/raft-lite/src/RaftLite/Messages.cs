namespace RaftLite;

public interface IMessage { int Term { get; } }

public sealed record RequestVote(int Term, string CandidateId) : IMessage;
public sealed record VoteReply(int Term, bool Granted) : IMessage;            // who voted comes from the transport's "from"
public sealed record AppendEntries(int Term, string LeaderId) : IMessage;     // heartbeat: no log entries in this "lite" version
public sealed record AppendReply(int Term) : IMessage;

public enum Role { Follower, Candidate, Leader }

public interface IRaftTransport { void Send(string to, IMessage message); }

public sealed record RaftOptions(int ElectionMinMs = 150, int ElectionMaxMs = 300, int HeartbeatMs = 50);
