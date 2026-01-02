namespace Assignment.Domain.AssigneeSelection.Factories;

public interface IAssigneeSelectorFactory
{
    IAssigneeSelector GetSelector(AssigneeSelectionStrategy strategy);
}