using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace TodoListApp.Models;

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public string Tags { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; } = null;

    // Property for UI binding - not serialized to JSON
    [JsonIgnore]
    public List<string> TagsList => GetTagsList();

    // Check if the task is overdue
    [JsonIgnore]
    public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.Now.Date && !IsCompleted;

    // Format due date for display
    [JsonIgnore]
    public string DueDateDisplay => DueDate.HasValue ? DueDate.Value.ToString("MMM dd, yyyy") : string.Empty;

    // Helper method to get tags as a list
    public List<string> GetTagsList()
    {
        if (string.IsNullOrWhiteSpace(Tags))
            return new List<string>();

        return Tags.Split(',')
                   .Select(tag => tag.Trim())
                   .Where(tag => !string.IsNullOrWhiteSpace(tag))
                   .Distinct()
                   .ToList();
    }

    // Helper method to set tags from a list
    public void SetTagsFromList(IEnumerable<string> tags)
    {
        Tags = string.Join(", ", tags.Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct());
    }
}
