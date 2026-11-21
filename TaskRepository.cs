using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace TaskTracker;

public sealed class TaskRepository
{
    private readonly string _connectionString;

    public string DatabasePath { get; }

    public TaskRepository()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskTracker");
        DatabasePath = Path.Combine(directory, "tasks.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath
        }.ToString();
    }

    public void Initialize()
    {
        if (!File.Exists(DatabasePath))
        {
            throw new FileNotFoundException("The existing task database was not found.", DatabasePath);
        }

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Tasks);";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        var requiredColumns = new[] { "Id", "Title", "Notes", "DueDate", "Priority", "IsComplete", "CreatedAt" };
        var missingColumns = requiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidDataException(
                $"The task database schema is missing required column(s): {string.Join(", ", missingColumns)}.");
        }
    }

    public List<TaskItem> GetAll()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Title, Notes, DueDate, Priority, IsComplete, CreatedAt
            FROM Tasks
            ORDER BY IsComplete, DueDate IS NULL, DueDate, Id DESC;
            """;

        using var reader = command.ExecuteReader();
        var tasks = new List<TaskItem>();
        while (reader.Read())
        {
            var dueDateText = reader.IsDBNull(3) ? null : reader.GetString(3);
            DateTime? dueDate = dueDateText is null
                ? null
                : DateTime.Parse(dueDateText, CultureInfo.InvariantCulture);
            var createdAt = DateTime.Parse(
                reader.GetString(6),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

            tasks.Add(new TaskItem
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Notes = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                DueDate = dueDate,
                Priority = reader.GetInt32(4),
                IsComplete = reader.GetInt64(5) != 0,
                CreatedAt = createdAt
            });
        }

        return tasks;
    }

    public void Add(TaskItem task)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tasks (Title, Notes, DueDate, Priority, IsComplete, CreatedAt)
            VALUES ($title, $notes, $dueDate, $priority, $isComplete, $createdAt);
            """;
        AddTaskParameters(command, task);
        command.Parameters.AddWithValue("$isComplete", task.IsComplete ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public void Update(TaskItem task)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Tasks
            SET Title = $title, Notes = $notes, DueDate = $dueDate, Priority = $priority
            WHERE Id = $id;
            """;
        AddTaskParameters(command, task);
        command.Parameters.AddWithValue("$id", task.Id);
        command.ExecuteNonQuery();
    }

    public void SetCompletion(int taskId, bool isCompleted)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tasks SET IsComplete = $isComplete WHERE Id = $id;";
        command.Parameters.AddWithValue("$isComplete", isCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$id", taskId);
        command.ExecuteNonQuery();
    }

    public void Delete(int taskId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tasks WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", taskId);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void AddTaskParameters(SqliteCommand command, TaskItem task)
    {
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$notes", task.Notes);
        command.Parameters.AddWithValue("$priority", task.Priority);
        var dueDate = task.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        command.Parameters.AddWithValue("$dueDate", (object?)dueDate ?? DBNull.Value);
    }
}
