namespace NihomeBackend.Services;

public sealed class AggregateDeletionBlockedException(string message) : Exception(message);