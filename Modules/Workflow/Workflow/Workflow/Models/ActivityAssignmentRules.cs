namespace Workflow.Workflow.Models;

public record ActivityAssignmentRules(
    bool TeamConstrained,
    List<string> ExcludeAssigneesFrom)
{
    public static ActivityAssignmentRules Default => new(false, []);
}
