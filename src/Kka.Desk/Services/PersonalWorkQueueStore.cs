using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Kka.Desk.Services;

/// <summary>
/// Stores generic personal reminders for the current Windows user. This queue is not a
/// client-work cache and must never contain client names, identifiers, documents, or credentials.
/// </summary>
public sealed class PersonalWorkQueueStore
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumItemCount = 500;
    private const int MaximumAuditCount = 2_000;
    private const long MaximumFileBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions StorageOptions = CreateStorageOptions();

    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly string _mutexName;
    private readonly TimeProvider _timeProvider;

    public PersonalWorkQueueStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KKA Desk", "personal-work-queue.json"))
    {
    }

    public PersonalWorkQueueStore(string filePath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
        var pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_filePath.ToUpperInvariant())));
        _mutexName = $"Local\\KkaDesk.PersonalWorkQueue.{pathHash}";
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public WorkQueueSnapshot Load() =>
        WithFileLock(() => ToSnapshot(ReadDocument()));

    public PersonalWorkItem Add(
        string title,
        WorkItemCategory category,
        WorkItemPriority priority,
        DateOnly? dueOn)
    {
        var normalizedTitle = PersonalWorkItemRules.NormalizeTitle(title);
        if (!Enum.IsDefined(category))
            throw new ArgumentOutOfRangeException(nameof(category));
        if (!Enum.IsDefined(priority))
            throw new ArgumentOutOfRangeException(nameof(priority));

        return WithFileLock(() =>
        {
            var document = ReadDocument();
            if (document.Items.Count >= MaximumItemCount)
                throw new InvalidOperationException("The personal reminder queue is full.");

            var item = new PersonalWorkItem(
                Guid.NewGuid(), normalizedTitle, category, priority, dueOn,
                _timeProvider.GetUtcNow(), null);
            document.Items.Add(item);
            AddAuditEvent(document, item.Id, WorkQueueAction.Created);
            SaveDocument(document);
            return item;
        });
    }

    public bool SetCompleted(Guid itemId, bool isCompleted)
    {
        return WithFileLock(() =>
        {
            var document = ReadDocument();
            var index = document.Items.FindIndex(item => item.Id == itemId);
            if (index < 0)
                return false;

            var current = document.Items[index];
            if (current.IsCompleted == isCompleted)
                return false;

            var updated = current with
            {
                CompletedAtUtc = isCompleted ? _timeProvider.GetUtcNow() : null
            };
            document.Items[index] = updated;
            AddAuditEvent(document, itemId,
                isCompleted ? WorkQueueAction.Completed : WorkQueueAction.Reopened);
            SaveDocument(document);
            return true;
        });
    }

    public bool Delete(Guid itemId)
    {
        return WithFileLock(() =>
        {
            var document = ReadDocument();
            var index = document.Items.FindIndex(item => item.Id == itemId);
            if (index < 0)
                return false;

            document.Items.RemoveAt(index);
            AddAuditEvent(document, itemId, WorkQueueAction.Deleted);
            SaveDocument(document);
            return true;
        });
    }

    private T WithFileLock<T>(Func<T> operation)
    {
        lock (_sync)
        {
            using var mutex = new Mutex(false, _mutexName);
            var ownsMutex = false;
            try
            {
                try
                {
                    ownsMutex = mutex.WaitOne(TimeSpan.FromSeconds(10));
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }

                if (!ownsMutex)
                    throw new TimeoutException("The personal reminder file is busy in another KKA Desk window.");

                return operation();
            }
            finally
            {
                if (ownsMutex)
                    mutex.ReleaseMutex();
            }
        }
    }

    private WorkQueueDocument ReadDocument()
    {
        if (!File.Exists(_filePath))
            return new WorkQueueDocument();

        var file = new FileInfo(_filePath);
        if (file.Length is <= 0 or > MaximumFileBytes)
            throw new InvalidDataException("The local reminder file has an invalid size.");

        WorkQueueDocument? document;
        try
        {
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            document = JsonSerializer.Deserialize<WorkQueueDocument>(stream, StorageOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The local reminder file could not be read.", exception);
        }

        if (document is null || document.SchemaVersion != CurrentSchemaVersion ||
            document.Items is null || document.Audit is null)
            throw new InvalidDataException("The local reminder file has an unsupported format.");

        ValidateDocument(document);
        return document;
    }

    private static void ValidateDocument(WorkQueueDocument document)
    {
        if (document.Items.Count > MaximumItemCount || document.Audit.Count > MaximumAuditCount)
            throw new InvalidDataException("The local reminder file exceeds its supported limits.");

        var itemIds = new HashSet<Guid>();
        foreach (var item in document.Items)
        {
            if (item is null || item.Id == Guid.Empty || !itemIds.Add(item.Id) ||
                !Enum.IsDefined(item.Category) || !Enum.IsDefined(item.Priority) ||
                item.CreatedAtUtc == default ||
                (item.CompletedAtUtc is not null && item.CompletedAtUtc < item.CreatedAtUtc))
                throw new InvalidDataException("The local reminder file contains an invalid reminder.");

            try
            {
                _ = PersonalWorkItemRules.NormalizeTitle(item.Title);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("The local reminder file contains an invalid reminder.", exception);
            }
        }

        foreach (var auditEvent in document.Audit)
        {
            if (auditEvent is null || auditEvent.ItemId == Guid.Empty ||
                auditEvent.OccurredAtUtc == default || !Enum.IsDefined(auditEvent.Action))
                throw new InvalidDataException("The local reminder audit history is invalid.");
        }
    }

    private void SaveDocument(WorkQueueDocument document)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, document, StorageOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private void AddAuditEvent(WorkQueueDocument document, Guid itemId, WorkQueueAction action)
    {
        document.Audit.Add(new WorkQueueAuditEvent(itemId, action, _timeProvider.GetUtcNow()));
        if (document.Audit.Count > MaximumAuditCount)
            document.Audit.RemoveRange(0, document.Audit.Count - MaximumAuditCount);
    }

    private static WorkQueueSnapshot ToSnapshot(WorkQueueDocument document) =>
        new(document.Items.ToArray(), document.Audit.ToArray());

    private static JsonSerializerOptions CreateStorageOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed class WorkQueueDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<PersonalWorkItem> Items { get; set; } = [];
        public List<WorkQueueAuditEvent> Audit { get; set; } = [];
    }
}

public sealed record PersonalWorkItem(
    Guid Id,
    string Title,
    WorkItemCategory Category,
    WorkItemPriority Priority,
    DateOnly? DueOn,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    [JsonIgnore]
    public bool IsCompleted => CompletedAtUtc is not null;

    [JsonIgnore]
    public string DueDateLabel => DueOn?.ToString("d MMM yyyy", CultureInfo.CurrentCulture) ?? "—";

    [JsonIgnore]
    public string CompletionActionLabel => IsCompleted ? "Reopen" : "Complete";

    [JsonIgnore]
    public string StatusLabel => IsCompleted ? "Complete" : "Open";
}

public sealed record WorkQueueAuditEvent(Guid ItemId, WorkQueueAction Action, DateTimeOffset OccurredAtUtc);

public sealed record WorkQueueSnapshot(
    IReadOnlyList<PersonalWorkItem> Items,
    IReadOnlyList<WorkQueueAuditEvent> AuditEvents);

public enum WorkItemCategory
{
    Operations,
    Learning,
    Personal
}

public enum WorkItemPriority
{
    Low,
    Normal,
    High
}

public enum WorkQueueAction
{
    Created,
    Completed,
    Reopened,
    Deleted
}

public static class PersonalWorkItemRules
{
    public const int MaximumTitleLength = 120;

    private static readonly Regex[] SensitiveIdentifierPatterns =
    [
        new(@"\b[A-Z]{5}[0-9]{4}[A-Z]\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b[^@\s]+@[^@\s]+\.[^@\s]+\b", RegexOptions.CultureInvariant),
        new(@"(?<!\d)(?:\+?91[\s-]?)?[6-9]\d{9}(?!\d)", RegexOptions.CultureInvariant),
        new(@"(?<!\d)\d{12}(?!\d)", RegexOptions.CultureInvariant)
    ];

    public static string NormalizeTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var normalized = title.Trim();
        if (normalized.Length > MaximumTitleLength)
            throw new ArgumentException($"Keep reminders to {MaximumTitleLength} characters or fewer.", nameof(title));
        if (normalized.Any(char.IsControl))
            throw new ArgumentException("Use a single-line reminder title.", nameof(title));
        var compactTitle = Regex.Replace(normalized, @"[\s()-]", string.Empty);
        if (SensitiveIdentifierPatterns.Any(pattern =>
                pattern.IsMatch(normalized) || pattern.IsMatch(compactTitle)))
            throw new ArgumentException(
                "Use a general reminder. Client contact and tax identifiers cannot be saved here.", nameof(title));
        return normalized;
    }
}
