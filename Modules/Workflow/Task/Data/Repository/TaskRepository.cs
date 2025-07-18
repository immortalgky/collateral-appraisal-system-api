namespace Task.Data.Repository;

public class TaskRepository(TaskDbContext dbContext) : ITaskRepository
{
    public async Task<bool> AddTaskAsync(Tasks.Models.Task task, CancellationToken cancellationToken = default)
    {
        dbContext.Tasks.Add(task);
        
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}