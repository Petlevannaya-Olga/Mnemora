using System.Collections.ObjectModel;

namespace Mnemora.Desktop.Notifications;

public interface INotificationService
{
    ReadOnlyObservableCollection<NotificationMessage> Notifications { get; }

    void ShowSuccess(string message);

    void ShowError(string message);

    void ShowWarning(string message);

    void ShowInformation(string message);

    void Dismiss(Guid notificationId);
}