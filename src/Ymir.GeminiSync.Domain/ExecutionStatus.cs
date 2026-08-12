namespace Ymir.GeminiSync.Domain;

public enum ExecutionStatus
{
    Running,
    Completed,
    Partial,
    Cancelled,
    Deleted = 99
}
