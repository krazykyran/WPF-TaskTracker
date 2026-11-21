using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;

namespace TaskTracker;

public partial class MainWindow : Window
{
    private readonly TaskRepository _repository = new();

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        PriorityComboBox.SelectedIndex = 1;

        if (TryDatabaseAction(() =>
            {
                _repository.Initialize();
                ReloadTasks();
            }))
        {
            StatusText.Text = "Ready. Select a task to edit its details.";
            DatabaseLocationText.Text = $"Database file: {_repository.DatabasePath}";
        }
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text.Trim();
        if (title.Length == 0)
        {
            StatusText.Text = "Enter a title before adding a task.";
            TitleTextBox.Focus();
            return;
        }

        var task = new TaskItem
        {
            Title = title,
            Notes = NotesTextBox.Text.Trim(),
            Priority = GetSelectedPriority(),
            DueDate = DueDatePicker.SelectedDate
        };

        if (TryDatabaseAction(() =>
            {
                _repository.Add(task);
                ReloadTasks();
            }))
        {
            ClearForm();
            StatusText.Text = "Task added.";
        }
    }

    private void SaveTask_Click(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskItem selectedTask)
        {
            StatusText.Text = "Select a task in the list before saving changes.";
            return;
        }

        var title = TitleTextBox.Text.Trim();
        if (title.Length == 0)
        {
            StatusText.Text = "A task title cannot be empty.";
            TitleTextBox.Focus();
            return;
        }

        var updatedTask = new TaskItem
        {
            Id = selectedTask.Id,
            Title = title,
            Notes = NotesTextBox.Text.Trim(),
            Priority = GetSelectedPriority(),
            DueDate = DueDatePicker.SelectedDate,
            IsComplete = selectedTask.IsComplete,
            CreatedAt = selectedTask.CreatedAt
        };

        if (TryDatabaseAction(() =>
            {
                _repository.Update(updatedTask);
                ReloadTasks();
            }))
        {
            ClearForm();
            StatusText.Text = "Changes saved.";
        }
    }

    private void ToggleCompletion_Click(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskItem selectedTask)
        {
            StatusText.Text = "Select a task to mark done or reopen.";
            return;
        }

        var isCompleted = !selectedTask.IsComplete;
        if (TryDatabaseAction(() =>
            {
                _repository.SetCompletion(selectedTask.Id, isCompleted);
                ReloadTasks();
            }))
        {
            ClearForm();
            StatusText.Text = isCompleted ? "Task marked done." : "Task reopened.";
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskItem selectedTask)
        {
            StatusText.Text = "Select a task to delete.";
            return;
        }

        var result = MessageBox.Show(
            $"Delete '{selectedTask.Title}'? This cannot be undone.",
            "Delete task",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (TryDatabaseAction(() =>
            {
                _repository.Delete(selectedTask.Id);
                ReloadTasks();
            }))
        {
            ClearForm();
            StatusText.Text = "Task deleted.";
        }
    }

    private void ClearForm_Click(object sender, RoutedEventArgs e)
    {
        ClearForm();
        StatusText.Text = "Form cleared.";
    }

    private void TaskGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskItem task)
        {
            return;
        }

        TitleTextBox.Text = task.Title;
        NotesTextBox.Text = task.Notes;
            PriorityComboBox.SelectedIndex = task.Priority switch
            {
                0 => 0,
                2 => 2,
                _ => 1
            };
        DueDatePicker.SelectedDate = task.DueDate;
        StatusText.Text = "Editing the selected task. Use Save changes to keep your edits.";
    }

    private int GetSelectedPriority()
    {
        return PriorityComboBox.SelectedIndex switch
        {
            0 => 0,
            2 => 2,
            _ => 1
        };
    }

    private void ReloadTasks()
    {
        Tasks.Clear();
        foreach (var task in _repository.GetAll())
        {
            Tasks.Add(task);
        }
    }

    private void ClearForm()
    {
        TaskGrid.SelectedItem = null;
        TitleTextBox.Clear();
        NotesTextBox.Clear();
        PriorityComboBox.SelectedIndex = 1;
        DueDatePicker.SelectedDate = null;
    }

    private bool TryDatabaseAction(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (SqliteException exception)
        {
            ShowDatabaseError(exception);
        }
        catch (IOException exception)
        {
            ShowDatabaseError(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowDatabaseError(exception);
        }
        catch (InvalidDataException exception)
        {
            ShowDatabaseError(exception);
        }

        return false;
    }

    private void ShowDatabaseError(Exception exception)
    {
        StatusText.Text = "The database operation failed.";
        MessageBox.Show(
            $"The task database could not be accessed.\n\n{exception.Message}",
            "Database error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
