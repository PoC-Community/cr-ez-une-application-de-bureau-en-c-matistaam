using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp;

public partial class MainWindow : Window
{
    private ObservableCollection<TaskItem> _tasks = new();
    private const string DataFolder = "data";
    private const string DataFile = "data/tasks.json";

    public MainWindow()
    {
        InitializeComponent();
        TaskList.ItemsSource = _tasks;

        AddButton.Click += OnAddClick;
        DeleteButton.Click += OnDeleteClick;
        SaveButton.Click += OnSaveClick;

        // Load tasks on startup
        LoadTasks();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TaskInput.Text))
        {
            _tasks.Add(new TaskItem { Title = TaskInput.Text });
            TaskInput.Text = string.Empty;
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is TaskItem selected)
        {
            _tasks.Remove(selected);
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
    }
}
