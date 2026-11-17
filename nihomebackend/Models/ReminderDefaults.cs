namespace NihomeBackend.Models;

/// <summary>Per-record "remind N days before the due date" settings shared by tenders and quotes.</summary>
public static class ReminderDefaults
{
    public const int DaysBefore = 3;
    public const int MaxDaysBefore = 365;
}
