namespace LinePilot.Services;

/// <summary>A business rule was broken. Controllers show the message to the user.</summary>
public class DomainException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);
