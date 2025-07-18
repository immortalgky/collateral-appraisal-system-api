namespace Task.Data.Repository;

public interface ITaskRepository
{
    Task<bool> AddTaskAsync(Tasks.Models.Task task, CancellationToken cancellationToken = default);
}