using Auth.Domain.Organization;

namespace Auth.Tests.Domain;

public class DepartmentTests
{
    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_SetsCode()
    {
        var dept = Department.Create("D001");
        Assert.Equal("D001", dept.Code);
    }

    [Fact]
    public void Create_SetsOptionalFields()
    {
        var dept = Department.Create("D001", divisionCode: "DIV1", description: "Finance");
        Assert.Equal("DIV1", dept.DivisionCode);
        Assert.Equal("Finance", dept.Description);
    }

    [Fact]
    public void Create_DefaultsIsActiveTrue()
    {
        var dept = Department.Create("D001");
        Assert.True(dept.IsActive);
    }

    [Fact]
    public void Create_DefaultsLastSyncedAtNull()
    {
        var dept = Department.Create("D001");
        Assert.Null(dept.LastSyncedAt);
    }

    [Fact]
    public void Create_WithNullOptionalFields_DoesNotThrow()
    {
        var dept = Department.Create("D002");
        Assert.Null(dept.DivisionCode);
        Assert.Null(dept.Description);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_MutatesDivisionCodeAndDescription()
    {
        var dept = Department.Create("D001", divisionCode: "OLD", description: "Old Desc");
        dept.Update("NEW", "New Desc", isActive: true);
        Assert.Equal("NEW", dept.DivisionCode);
        Assert.Equal("New Desc", dept.Description);
    }

    [Fact]
    public void Update_CanDeactivate()
    {
        var dept = Department.Create("D001");
        dept.Update(null, null, isActive: false);
        Assert.False(dept.IsActive);
    }

    [Fact]
    public void Update_CanReactivate()
    {
        var dept = Department.Create("D001");
        dept.Update(null, null, isActive: false);
        dept.Update(null, null, isActive: true);
        Assert.True(dept.IsActive);
    }

    // ── MarkSynced ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkSynced_SetsLastSyncedAt()
    {
        var dept = Department.Create("D001");
        var syncTime = new DateTime(2026, 1, 15, 8, 0, 0);
        dept.MarkSynced(syncTime);
        Assert.Equal(syncTime, dept.LastSyncedAt);
    }

    [Fact]
    public void MarkSynced_OverwritesPreviousValue()
    {
        var dept = Department.Create("D001");
        dept.MarkSynced(new DateTime(2026, 1, 1));
        var later = new DateTime(2026, 6, 27, 9, 0, 0);
        dept.MarkSynced(later);
        Assert.Equal(later, dept.LastSyncedAt);
    }
}
