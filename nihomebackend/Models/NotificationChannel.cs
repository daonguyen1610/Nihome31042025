namespace NihomeBackend.Models;

/// <summary>
/// Delivery channel for a notification template. A template can be routed to
/// in-app only, email only, or both. Business rules that only care about the
/// activity feed use <see cref="InApp"/>; rules that must reach the user via
/// email regardless of whether they are logged in use <see cref="Email"/>;
/// most mixed cases use <see cref="Both"/>.
/// </summary>
public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
    Both = 2,
}
