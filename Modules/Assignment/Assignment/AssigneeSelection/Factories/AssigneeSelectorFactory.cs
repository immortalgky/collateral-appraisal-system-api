namespace Assignment.AssigneeSelection.Factories;

public class AssigneeSelectorFactory : IAssigneeSelectorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AssigneeSelectorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Provides an implementation of the <see cref="IAssigneeSelector"/> based on the specified selection strategy.
    /// </summary>
    /// <param name="strategy">The strategy specifying how the assignee should be selected. Supported strategies include Manual, RoundRobin, WorkloadBased, and Random.</param>
    /// <returns>An instance of a class implementing <see cref="IAssigneeSelector"/> corresponding to the specified strategy.</returns>
    /// <exception cref="ArgumentException">Thrown when the specified strategy is not recognized.</exception>
    public IAssigneeSelector GetSelector(AssigneeSelectionStrategy strategy)
    {
        return strategy switch
        {
            AssigneeSelectionStrategy.Manual => _serviceProvider.GetRequiredService<ManualAssigneeSelector>(),
            AssigneeSelectionStrategy.RoundRobin => _serviceProvider.GetRequiredService<RoundRobinAssigneeSelector>(),
            AssigneeSelectionStrategy.WorkloadBased => _serviceProvider
                .GetRequiredService<WorkloadBasedAssigneeSelector>(),
            AssigneeSelectionStrategy.Random => _serviceProvider.GetRequiredService<RandomAssigneeSelector>(),
            _ => throw new ArgumentException($"Unknown assignee selection strategy: {strategy}")
        };
    }
}