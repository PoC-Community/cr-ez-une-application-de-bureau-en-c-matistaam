using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp;

public partial class MainWindow : Window
{
    private ObservableCollection<TaskItem> _tasks = new();
    private ObservableCollection<TaskItem> _filteredTasks = new();
    private const string DataFolder = "data";
    private const string DataFile = "data/tasks.json";
    private const string BackupFile = "data/tasks.backup.json";
    private string? _currentFilter = null;
    private string? _currentDateFilter = null;
    private System.Threading.Timer? _autoSaveTimer;
    private bool _hasUnsavedChanges = false;

    public MainWindow()
    {
        InitializeComponent();
        TaskList.ItemsSource = _filteredTasks;

        AddButton.Click += OnAddClick;
        DeleteButton.Click += OnDeleteClick;
        SaveButton.Click += OnSaveClick;
        FilterButton.Click += OnFilterClick;
        ClearFilterButton.Click += OnClearFilterClick;
        FilterTodayButton.Click += OnFilterTodayClick;
        FilterWeekButton.Click += OnFilterWeekClick;
        FilterOverdueButton.Click += OnFilterOverdueClick;
        CompleteAllButton.Click += OnCompleteAllClick;
        ClearCompletedButton.Click += OnClearCompletedClick;

        // Set up auto-save timer (saves every 5 seconds if there are changes)
        _autoSaveTimer = new System.Threading.Timer(
            AutoSaveCallback,
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5)
        );

        // Handle window closing to save and cleanup
        this.Closing += OnWindowClosing;

        // Load tasks on startup
        LoadTasks();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save on close if there are unsaved changes
        if (_hasUnsavedChanges)
        {
            SaveTasks();
        }

        // Clean up timer
        _autoSaveTimer?.Dispose();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TaskInput.Text))
        {
            var newTask = new TaskItem 
            { 
                Title = TaskInput.Text,
                Tags = TagsInput.Text ?? string.Empty,
                DueDate = DueDatePicker.SelectedDate?.DateTime
            };
            _tasks.Add(newTask);
            TaskInput.Text = string.Empty;
            TagsInput.Text = string.Empty;
            DueDatePicker.SelectedDate = null;
            
            // Mark as having unsaved changes
            _hasUnsavedChanges = true;
            UpdateSaveStatus("unsaved");
            
            // Refresh filtered view
            ApplyFilter();
        }
    }

    private void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        _currentFilter = FilterTagInput.Text?.Trim();
        ApplyFilter();
    }

    private void OnClearFilterClick(object? sender, RoutedEventArgs e)
    {
        _currentFilter = null;
        _currentDateFilter = null;
        FilterTagInput.Text = string.Empty;
        ApplyFilter();
    }

    private void OnFilterTodayClick(object? sender, RoutedEventArgs e)
    {
        _currentDateFilter = "today";
        ApplyFilter();
    }

    private void OnFilterWeekClick(object? sender, RoutedEventArgs e)
    {
        _currentDateFilter = "week";
        ApplyFilter();
    }

    private void OnFilterOverdueClick(object? sender, RoutedEventArgs e)
    {
        _currentDateFilter = "overdue";
        ApplyFilter();
    }

    private void OnCompleteAllClick(object? sender, RoutedEventArgs e)
    {
        foreach (var task in _tasks)
        {
            task.IsCompleted = true;
        }
        _hasUnsavedChanges = true;
        UpdateSaveStatus("unsaved");
        // Refresh the view to show updated checkboxes
        ApplyFilter();
    }

    private void OnClearCompletedClick(object? sender, RoutedEventArgs e)
    {
        // Remove all completed tasks
        var completedTasks = _tasks.Where(t => t.IsCompleted).ToList();
        foreach (var task in completedTasks)
        {
            _tasks.Remove(task);
        }
        _hasUnsavedChanges = true;
        UpdateSaveStatus("unsaved");
        ApplyFilter();
    }

    private void OnTaskTitleDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.Parent is StackPanel panel)
        {
            // Find the TextBox in the same panel
            var textBox = panel.Children.OfType<TextBox>().FirstOrDefault(tb => tb.Name == "TaskTitleEdit");
            if (textBox != null)
            {
                textBlock.IsVisible = false;
                textBox.IsVisible = true;
                textBox.Focus();
                textBox.SelectAll();
            }
        }
    }

    private void OnTaskTitleEditLostFocus(object? sender, RoutedEventArgs e)
    {
        FinishEditingTitle(sender);
    }

    private void OnTaskTitleEditKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            FinishEditingTitle(sender);
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.Escape)
        {
            CancelEditingTitle(sender);
            e.Handled = true;
        }
    }

    private void FinishEditingTitle(object? sender)
    {
        if (sender is TextBox textBox && textBox.Parent is StackPanel panel)
        {
            // Update the task title
            if (textBox.DataContext is TaskItem task && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                task.Title = textBox.Text;
                _hasUnsavedChanges = true;
                UpdateSaveStatus("unsaved");
            }

            // Switch back to TextBlock
            var textBlock = panel.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Name == "TaskTitleText");
            if (textBlock != null)
            {
                textBox.IsVisible = false;
                textBlock.IsVisible = true;
            }

            // Refresh the view
            ApplyFilter();
        }
    }

    private void CancelEditingTitle(object? sender)
    {
        if (sender is TextBox textBox && textBox.Parent is StackPanel panel)
        {
            // Reset the text to original value
            if (textBox.DataContext is TaskItem task)
            {
                textBox.Text = task.Title;
            }

            // Switch back to TextBlock without saving
            var textBlock = panel.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Name == "TaskTitleText");
            if (textBlock != null)
            {
                textBox.IsVisible = false;
                textBlock.IsVisible = true;
            }
        }
    }

    private void ApplyFilter()
    {
        _filteredTasks.Clear();

        foreach (var task in _tasks)
        {
            bool matchesTagFilter = true;
            bool matchesDateFilter = true;

            // Apply tag filter
            if (!string.IsNullOrWhiteSpace(_currentFilter))
            {
                var filterTag = _currentFilter.ToLower();
                var taskTags = task.GetTagsList().Select(t => t.ToLower());
                matchesTagFilter = taskTags.Any(tag => tag.Contains(filterTag));
            }

            // Apply date filter
            if (!string.IsNullOrWhiteSpace(_currentDateFilter))
            {
                matchesDateFilter = _currentDateFilter switch
                {
                    "today" => task.DueDate.HasValue && task.DueDate.Value.Date == DateTime.Now.Date,
                    "week" => task.DueDate.HasValue && task.DueDate.Value.Date >= DateTime.Now.Date && 
                              task.DueDate.Value.Date <= DateTime.Now.AddDays(7).Date,
                    "overdue" => task.IsOverdue,
                    _ => true
                };
            }

            // Add task if it matches all active filters
            if (matchesTagFilter && matchesDateFilter)
            {
                _filteredTasks.Add(task);
            }
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is TaskItem selected)
        {
            _tasks.Remove(selected);
            _hasUnsavedChanges = true;
            UpdateSaveStatus("unsaved");
            ApplyFilter();
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        SaveTasks();
    }

    private void AutoSaveCallback(object? state)
    {
        if (_hasUnsavedChanges)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SaveTasks());
        }
    }

    private void SaveTasks()
    {
        try
        {
            // Update status indicator - saving
            UpdateSaveStatus("saving");

            // Create the data folder if it doesn't exist
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            // Create a backup of the existing file before overwriting
            if (File.Exists(DataFile))
            {
                try
                {
                    File.Copy(DataFile, BackupFile, true);
                }
                catch (Exception backupEx)
                {
                    System.Console.WriteLine($"Warning: Could not create backup: {backupEx.Message}");
                }
            }

            // Serialize the tasks to JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_tasks, options);

            // Write to file
            File.WriteAllText(DataFile, json);

            _hasUnsavedChanges = false;
            System.Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Tasks saved successfully (auto-save)");
            
            // Update status indicator - saved
            UpdateSaveStatus("saved");
        }
        catch (UnauthorizedAccessException)
        {
            System.Console.WriteLine("Error: Permission denied. Please check file permissions.");
            UpdateSaveStatus("error");
        }
        catch (IOException ex)
        {
            System.Console.WriteLine($"Error: Disk I/O error - {ex.Message}");
            UpdateSaveStatus("error");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error saving tasks: {ex.Message}");
            UpdateSaveStatus("error");
        }
    }

    private void UpdateSaveStatus(string status)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            switch (status)
            {
                case "saved":
                    SaveStatusText.Foreground = Avalonia.Media.Brushes.Green;
                    Avalonia.Controls.ToolTip.SetTip(SaveStatusText, "All changes saved");
                    break;
                case "saving":
                    SaveStatusText.Foreground = Avalonia.Media.Brushes.Orange;
                    Avalonia.Controls.ToolTip.SetTip(SaveStatusText, "Saving...");
                    break;
                case "error":
                    SaveStatusText.Foreground = Avalonia.Media.Brushes.Red;
                    Avalonia.Controls.ToolTip.SetTip(SaveStatusText, "Error saving - check console");
                    break;
                case "unsaved":
                    SaveStatusText.Foreground = Avalonia.Media.Brushes.Yellow;
                    Avalonia.Controls.ToolTip.SetTip(SaveStatusText, "Unsaved changes");
                    break;
            }
        });
    }

    private void LoadTasks()
    {
        bool loadedSuccessfully = false;

        try
        {
            // Check if the file exists
            if (File.Exists(DataFile))
            {
                // Read the JSON file
                var json = File.ReadAllText(DataFile);

                // Validate JSON is not empty
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new JsonException("JSON file is empty");
                }

                // Deserialize the JSON into a list of TaskItem
                var tasks = JsonSerializer.Deserialize<List<TaskItem>>(json);

                // Clear existing tasks and add loaded tasks
                _tasks.Clear();
                if (tasks != null)
                {
                    foreach (var task in tasks)
                    {
                        _tasks.Add(task);
                    }
                    loadedSuccessfully = true;
                }

                System.Console.WriteLine($"✓ Tasks loaded successfully! ({tasks?.Count ?? 0} tasks)");
            }
            else
            {
                // File doesn't exist, start with an empty list
                System.Console.WriteLine("No saved tasks found. Starting with an empty list.");
                loadedSuccessfully = true; // Not an error
            }
        }
        catch (JsonException ex)
        {
            // Handle invalid JSON - try to recover from backup
            System.Console.WriteLine($"⚠ Error parsing JSON file: {ex.Message}");
            
            if (TryLoadBackup())
            {
                System.Console.WriteLine("✓ Successfully recovered from backup file.");
                loadedSuccessfully = true;
            }
            else
            {
                System.Console.WriteLine("⚠ Starting with an empty task list.");
            }
        }
        catch (UnauthorizedAccessException)
        {
            System.Console.WriteLine("✗ Error: Permission denied. Cannot read tasks file.");
        }
        catch (Exception ex)
        {
            // Handle other errors (permissions, etc.)
            System.Console.WriteLine($"✗ Error loading tasks: {ex.Message}");
            
            if (TryLoadBackup())
            {
                System.Console.WriteLine("✓ Successfully recovered from backup file.");
                loadedSuccessfully = true;
            }
            else
            {
                System.Console.WriteLine("⚠ Starting with an empty task list.");
            }
        }
        finally
        {
            // Always refresh the filtered view after loading
            ApplyFilter();
            
            if (!loadedSuccessfully && _tasks.Count == 0)
            {
                System.Console.WriteLine("ℹ You can start adding tasks now.");
            }
        }
    }

    private bool TryLoadBackup()
    {
        try
        {
            if (File.Exists(BackupFile))
            {
                var json = File.ReadAllText(BackupFile);
                var tasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
                
                if (tasks != null)
                {
                    _tasks.Clear();
                    foreach (var task in tasks)
                    {
                        _tasks.Add(task);
                    }
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Could not load backup: {ex.Message}");
        }
        
        return false;
    }
}
