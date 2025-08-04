namespace Assignment.AssigneeSelection.Factories;

public interface IAssigneeSelectorFactory
{
    IAssigneeSelector GetSelector(AssigneeSelectionStrategy strategy);
}