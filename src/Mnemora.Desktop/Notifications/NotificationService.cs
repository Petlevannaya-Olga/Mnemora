using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Mnemora.Desktop.Notifications;

public sealed class NotificationService : INotificationService, IDisposable
{
    private const int MaximumVisibleNotifications = 4;
    private static readonly TimeSpan NotificationLifetime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ExpirationCheckInterval = TimeSpan.FromMilliseconds(250);

    private readonly ObservableCollection<NotificationMessage> _notifications = [];
    private readonly TimeProvider _timeProvider;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _expirationTimer;
    private bool _isDisposed;

    public NotificationService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        Notifications = new ReadOnlyObservableCollection<NotificationMessage>(_notifications);

        _expirationTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = ExpirationCheckInterval
        };

        _expirationTimer.Tick += OnExpirationTimerTick;
    }

    public ReadOnlyObservableCollection<NotificationMessage> Notifications { get; }

    public void ShowSuccess(string message)
    {
        Show(message, NotificationKind.Success);
    }

    public void ShowError(string message)
    {
        Show(message, NotificationKind.Error);
    }

    public void ShowWarning(string message)
    {
        Show(message, NotificationKind.Warning);
    }

    public void ShowInformation(string message)
    {
        Show(message, NotificationKind.Information);
    }

    public void Dismiss(Guid notificationId)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        RunOnDispatcher(() =>
        {
            var notification = _notifications.FirstOrDefault(item => item.Id == notificationId);

            if (notification is not null)
            {
                _notifications.Remove(notification);
            }

            StopTimerIfQueueIsEmpty();
        });
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_dispatcher.CheckAccess())
        {
            DisposeCore();
        }
        else
        {
            _dispatcher.Invoke(DisposeCore);
        }
    }

    private void Show(string message, NotificationKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        RunOnDispatcher(() =>
        {
            while (_notifications.Count >= MaximumVisibleNotifications)
            {
                _notifications.RemoveAt(0);
            }

            _notifications.Add(new NotificationMessage(
                Guid.NewGuid(),
                message,
                kind,
                _timeProvider.GetUtcNow() + NotificationLifetime));

            if (!_expirationTimer.IsEnabled)
            {
                _expirationTimer.Start();
            }
        });
    }

    private void OnExpirationTimerTick(object? sender, EventArgs eventArgs)
    {
        var currentTime = _timeProvider.GetUtcNow();

        for (int index = _notifications.Count - 1; index >= 0; index--)
        {
            if (_notifications[index].ExpiresAt <= currentTime)
            {
                _notifications.RemoveAt(index);
            }
        }

        StopTimerIfQueueIsEmpty();
    }

    private void StopTimerIfQueueIsEmpty()
    {
        if (_notifications.Count == 0)
        {
            _expirationTimer.Stop();
        }
    }

    private void RunOnDispatcher(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }

    private void DisposeCore()
    {
        _expirationTimer.Stop();
        _expirationTimer.Tick -= OnExpirationTimerTick;
        _notifications.Clear();
    }
}