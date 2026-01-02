namespace Assignment.Services.Hashing;

public interface IGroupHashService
{
    string GenerateGroupsHash(List<string> userGroups);
    string GenerateGroupsList(List<string> userGroups);
}