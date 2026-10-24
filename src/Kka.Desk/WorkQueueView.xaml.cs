using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Kka.Desk.Services;

namespace Kka.Desk;

public partial class WorkQueueView : UserControl
{
    private WorkQueueSnapshot? _snapshot;
    private bool _isBusy;

    public WorkQueueView()
    {
        InitializeComponent();
        FilterBox.SelectedIndex = 0;
    }

    public PersonalWorkQueueStore? Store { get; set; }

    public event Action<WorkQueueSnapshot>? QueueChanged;
    public event Action? QueueLoadFailed;

    public async Task RefreshAsync()
    {
        if (Store is null)
        {
            ShowErrorState();
            return;
        }

        SetState(loading: true);
        try
        {
            _snapshot = await Task.Run(Store.Load);
            QueueChanged?.Invoke(_snapshot);
            SetState(loading: false);
            RenderItems();
            StatusText.Text = $"Saved on this Windows account · {_snapshot.Items.Count(item => !item.IsCompleted)} open";
        }
        catch (Exception)
        {
            _snapshot = null;
            SetState(loading: false);
            ShowErrorState();
            QueueLoadFailed?.Invoke();
            StatusText.Text = "Your reminders could not be loaded. The local file was left untouched.";
        }
    }

    private async void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        var store = Store;
        if (_isBusy || store is null)
            return;

        var dialog = new WorkItemDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;
        var draft = dialog.Draft;
        if (draft is null)
            return;

        await RunMutationAsync(() =>
        {
            store.Add(draft.Title, draft.Category, draft.Priority, draft.DueOn);
            return true;
        }, "Reminder added.");
    }

    private async void ToggleCompletion_Click(object sender, RoutedEventArgs e)
    {
        var store = Store;
        if (sender is not FrameworkElement { DataContext: PersonalWorkItem item } || store is null)
            return;

        await RunMutationAsync(() => store.SetCompleted(item.Id, !item.IsCompleted),
            item.IsCompleted ? "Reminder reopened." : "Reminder marked complete.");
    }

    private async void DeleteReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PersonalWorkItem item })
            return;
        await ConfirmAndDeleteAsync(item);
    }

    private async void Retry_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHintText.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        RenderItems();
    }

    private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RenderItems();

    private async void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            e.Handled = true;
            AddReminder_Click(this, new RoutedEventArgs());
            return;
        }

        var focus = Keyboard.FocusedElement as DependencyObject;
        var isEditingText = focus is TextBoxBase or ComboBox or DatePicker;
        if (e.Key == Key.Delete && !isEditingText && ItemsGrid.SelectedItem is PersonalWorkItem item)
        {
            e.Handled = true;
            await ConfirmAndDeleteAsync(item);
        }
    }

    private async Task ConfirmAndDeleteAsync(PersonalWorkItem item)
    {
        var store = Store;
        var owner = Window.GetWindow(this);
        var confirmation = new ConfirmationDialog(owner,
            "Delete reminder",
            $"Delete “{item.Title}”? This reminder will be removed from this Windows account.",
            "Delete reminder", "Keep reminder");
        if (confirmation.ShowDialog() != true || store is null)
            return;

        await RunMutationAsync(() => store.Delete(item.Id), "Reminder deleted.");
    }

    private async Task RunMutationAsync(Func<bool> mutation, string successMessage)
    {
        if (_isBusy)
            return;

        _isBusy = true;
        StatusText.Text = "Saving…";
        try
        {
            var changed = await Task.Run(mutation);
            if (!changed)
            {
                StatusText.Text = "This reminder changed or was removed. The list has been refreshed.";
                await RefreshAsync();
                return;
            }

            await RefreshAsync();
            if (_snapshot is not null)
                StatusText.Text = $"{successMessage} Saved on this Windows account.";
        }
        catch (Exception)
        {
            StatusText.Text = "Changes could not be saved. Your previous reminders were left intact.";
            ShowErrorState();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void RenderItems()
    {
        if (_snapshot is null)
            return;

        var query = SearchBox.Text.Trim();
        var filter = (FilterBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        IEnumerable<PersonalWorkItem> result = _snapshot.Items;

        if (filter == "Open")
            result = result.Where(item => !item.IsCompleted);
        else if (filter == "Completed")
            result = result.Where(item => item.IsCompleted);

        if (query.Length > 0)
            result = result.Where(item => item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        var items = result
            .OrderBy(item => item.IsCompleted)
            .ThenBy(item => item.DueOn ?? DateOnly.MaxValue)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        ItemsGrid.ItemsSource = items;
        var noFilter = string.IsNullOrWhiteSpace(query) && filter == "All reminders";
        ItemsGrid.Visibility = items.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = items.Length == 0 && _snapshot.Items.Count == 0 && noFilter
            ? Visibility.Visible : Visibility.Collapsed;
        NoResultsState.Visibility = items.Length == 0 && EmptyState.Visibility != Visibility.Visible
            ? Visibility.Visible : Visibility.Collapsed;
        ErrorState.Visibility = Visibility.Collapsed;
        LoadingState.Visibility = Visibility.Collapsed;
    }

    private void SetState(bool loading)
    {
        LoadingState.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        ItemsGrid.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        NoResultsState.Visibility = Visibility.Collapsed;
        ErrorState.Visibility = Visibility.Collapsed;
    }

    private void ShowErrorState()
    {
        SetState(loading: false);
        ErrorState.Visibility = Visibility.Visible;
    }
}
