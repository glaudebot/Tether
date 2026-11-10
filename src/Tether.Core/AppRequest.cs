namespace Tether.Core;

public record AppRequest(string From, string To, string Body);

public class RequestFormatException(string message) : Exception(message);
