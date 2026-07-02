using Auth.Domain.Organization;

namespace Auth.Tests.Domain;

public class OfficerTests
{
    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_SetsOfficerCode()
    {
        var officer = Officer.Create("O001");
        Assert.Equal("O001", officer.OfficerCode);
    }

    [Fact]
    public void Create_SetsAllOptionalFields()
    {
        var officer = Officer.Create(
            officerCode: "O001",
            branchNumber: "001",
            officerId: "john.doe",
            name: "John Doe",
            shortName: "JD",
            costCenterCode: "12345678",
            departmentCode: "D001");

        Assert.Equal("001", officer.BranchNumber);
        Assert.Equal("john.doe", officer.OfficerId);
        Assert.Equal("John Doe", officer.Name);
        Assert.Equal("JD", officer.ShortName);
        Assert.Equal("12345678", officer.CostCenterCode);
        Assert.Equal("D001", officer.DepartmentCode);
    }

    [Fact]
    public void Create_DefaultsIsActiveTrue()
    {
        var officer = Officer.Create("O001");
        Assert.True(officer.IsActive);
    }

    [Fact]
    public void Create_DefaultsLastSyncedAtNull()
    {
        var officer = Officer.Create("O001");
        Assert.Null(officer.LastSyncedAt);
    }

    [Fact]
    public void Create_WithNullOptionalFields_DoesNotThrow()
    {
        var officer = Officer.Create("O002");
        Assert.Null(officer.BranchNumber);
        Assert.Null(officer.OfficerId);
        Assert.Null(officer.Name);
        Assert.Null(officer.ShortName);
        Assert.Null(officer.CostCenterCode);
        Assert.Null(officer.DepartmentCode);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_MutatesAllFields()
    {
        var officer = Officer.Create("O001", branchNumber: "001", name: "Old Name");

        officer.Update(
            branchNumber: "002",
            officerId: "jane.doe",
            name: "Jane Doe",
            shortName: "JD",
            costCenterCode: "99999999",
            departmentCode: "D002",
            isActive: true);

        Assert.Equal("002", officer.BranchNumber);
        Assert.Equal("jane.doe", officer.OfficerId);
        Assert.Equal("Jane Doe", officer.Name);
        Assert.Equal("JD", officer.ShortName);
        Assert.Equal("99999999", officer.CostCenterCode);
        Assert.Equal("D002", officer.DepartmentCode);
    }

    [Fact]
    public void Update_CanDeactivate()
    {
        var officer = Officer.Create("O001");
        officer.Update(null, null, null, null, null, null, isActive: false);
        Assert.False(officer.IsActive);
    }

    [Fact]
    public void Update_CanReactivate()
    {
        var officer = Officer.Create("O001");
        officer.Update(null, null, null, null, null, null, isActive: false);
        officer.Update(null, null, null, null, null, null, isActive: true);
        Assert.True(officer.IsActive);
    }

    // ── MarkSynced ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkSynced_SetsLastSyncedAt()
    {
        var officer = Officer.Create("O001");
        var syncTime = new DateTime(2026, 3, 10, 12, 0, 0);
        officer.MarkSynced(syncTime);
        Assert.Equal(syncTime, officer.LastSyncedAt);
    }

    [Fact]
    public void MarkSynced_OverwritesPreviousValue()
    {
        var officer = Officer.Create("O001");
        officer.MarkSynced(new DateTime(2026, 1, 1));
        var later = new DateTime(2026, 6, 27, 9, 0, 0);
        officer.MarkSynced(later);
        Assert.Equal(later, officer.LastSyncedAt);
    }
}
