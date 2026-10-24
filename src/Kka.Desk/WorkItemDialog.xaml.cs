using System.Windows;
using System.Windows.Controls;
using Kka.Desk.Services;

namespace Kka.Desk;

public partial class WorkItemDialog : Window
{
    public WorkItemDialog()
    {
        InitializeComponent();
        CategoryBox.SelectedIndex = 0;
        PriorityBox.SelectedIndex = 1;
        Loaded += (_, _) => TitleBox.Focus();
    }

    public PersonalWorkItemDraft? Draft { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Visibility = Visibility.Collapsed;
        try
        {
            var title = PersonalWorkItemRules.NormalizeTitle(TitleBox.Text);
            if (!TryGetCategory(out var category))
                throw new ArgumentException("Choose a reminder type.");
            if (!TryGetPriority(out var priority))
                throw new ArgumentException("Choose a priority.");

            Draft = new PersonalWorkItemDraft(
                title, category, priority,
                DueDateBox.SelectedDate is { } selectedDate
                    ? DateOnly.FromDateTime(selectedDate)
                    : null);
            DialogResult = true;
        }
        catch (ArgumentException exception)
        {
            ValidationText.Text = exception.Message;
            ValidationText.Visibility = Visibility.Visible;
            TitleBox.Focus();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private bool TryGetCategory(out WorkItemCategory category) =>
        Enum.TryParse((CategoryBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out category);

    private bool TryGetPriority(out WorkItemPriority priority) =>
        Enum.TryParse((PriorityBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out priority);
}

public sealed record PersonalWorkItemDraft(
    string Title,
    WorkItemCategory Category,
    WorkItemPriority Priority,
    DateOnly? DueOn);
