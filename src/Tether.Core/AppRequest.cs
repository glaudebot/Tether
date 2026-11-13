namespace Tether.Core;

/// <summary>What the requester waits for: an image (default, waits for a new image) or a text answer.</summary>
public enum ReplyKind { Image, Text }

/// <summary>Whether the responder keeps the current conversation (default) or opens a new one first.</summary>
public enum ThreadMode { Continue, New }

public record AppRequest(string From, string To, string Body, ReplyKind Reply = ReplyKind.Image, ThreadMode Thread = ThreadMode.Continue);

public class RequestFormatException(string message) : Exception(message);
