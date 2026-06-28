using Auth.Domain.Organization;

namespace Auth.Tests.Domain;

public class CostCenterTests
{
    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_SetsCode()
    {
        var cc = CostCenter.Create("001");
        Assert.Equal("001", cc.Code);
    }

    [Fact]
    public void Create_SetsOptionalFields()
    {
        var cc = CostCenter.Create("001", description: "Head Office", text: "HO branch");
        Assert.Equal("Head Office", cc.Description);
        Assert.Equal("HO branch", cc.Text);
    }

    [Fact]
    public void Create_DefaultsIsActiveTrue()
    {
        var cc = CostCenter.Create("001");
        Assert.True(cc.IsActive);
    }

    [Fact]
    public void Create_DefaultsLastSyncedAtNull()
    {
        var cc = CostCenter.Create("001");
        Assert.Null(cc.LastSyncedAt);
    }

    [Fact]
    public void Create_WithNullOptionalFields_DoesNotThrow()
    {
        var cc = CostCenter.Create("002");
        Assert.Null(cc.Description);
        Assert.Null(cc.Text);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_MutatesDescriptionAndText()
    {
        var cc = CostCenter.Create("001", description: "Old Desc", text: "Old Text");
        cc.Update("New Desc", "New Text", isActive: true);
        Assert.Equal("New Desc", cc.Description);
        Assert.Equal("New Text", cc.Text);
    }

    [Fact]
    public void Update_CanDeactivate()
    {
        var cc = CostCenter.Create("001");
        cc.Update(null, null, isActive: false);
        Assert.False(cc.IsActive);
    }

    [Fact]
    public void Update_CanReactivate()
    {
        var cc = CostCenter.Create("001");
        cc.Update(null, null, isActive: false);
        cc.Update(null, null, isActive: true);
        Assert.True(cc.IsActive);
    }

    // ── MarkSynced ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkSynced_SetsLastSyncedAt()
    {
        var cc = CostCenter.Create("001");
        var syncTime = new DateTime(2026, 5, 20, 7, 30, 0);
        cc.MarkSynced(syncTime);
        Assert.Equal(syncTime, cc.LastSyncedAt);
    }

    [Fact]
    public void MarkSynced_OverwritesPreviousValue()
    {
        var cc = CostCenter.Create("001");
        cc.MarkSynced(new DateTime(2026, 1, 1));
        var later = new DateTime(2026, 6, 27, 9, 0, 0);
        cc.MarkSynced(later);
        Assert.Equal(later, cc.LastSyncedAt);
    }
}
