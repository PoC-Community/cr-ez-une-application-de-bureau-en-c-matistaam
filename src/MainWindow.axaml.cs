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
    private string? _currentFilter = null;
    private string? _currentDateFilter = null;

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

        // Load tasks on startup
        LoadTasks();
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
            ApplyFilter();
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Create the data folder if it doesn't exist
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            // Serialize the tasks to JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_tasks, options);

            // Write to file
            File.WriteAllText(DataFile, json);

            // Optional: Show a success message (you can add a TextBlock in the UI for this)
            System.Console.WriteLine("Tasks saved successfully!");
        }
        catch (Exception ex)
        {
            // Handle errors
            System.Console.WriteLine($"Error saving tasks: {ex.Message}");
        }
    }

    private void LoadTasks()
    {
        try
        {
            // Check if the file exists
            if (File.Exists(DataFile))
            {
                // Read the JSON file
                var json = File.ReadAllText(DataFile);

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
                }

                System.Console.WriteLine("Tasks loaded successfully!");
            }
            else
            {
                // File doesn't exist, start with an empty list
                System.Console.WriteLine("No saved tasks found. Starting with an empty list.");
            }
        }
        catch (JsonException ex)
        {
            // Handle invalid JSON
            System.Console.WriteLine($"Error parsing JSON file: {ex.Message}");
            System.Console.WriteLine("Starting with an empty task list.");
        }
        catch (Exception ex)
        {
            // Handle other errors (permissions, etc.)
            System.Console.WriteLine($"Error loading tasks: {ex.Message}");
            System.Console.WriteLine("Starting with an empty task list.");
        }
        finally
        {
            // Always refresh the filtered view after loading
            ApplyFilter();
        }
    }
}
