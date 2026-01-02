namespace Database.Migration;

public class MigrationHistory
{
    public int Id { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public string ScriptChecksum { get; set; } = string.Empty;
    public DateTime ExecutedOn { get; set; }
    public string ExecutedBy { get; set; } = string.Empty;
    public int ExecutionTimeMs { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}