namespace Tyflocentrum.Windows.Domain.Models;

public sealed class VoiceMessageRecorderNotificationEventArgs : EventArgs
{
    public VoiceMessageRecorderNotificationEventArgs(string message, bool isError)
    {
        Message = message;
        IsError = isError;
    }

    public string Message { get; }

    public bool IsError { get; }
}
