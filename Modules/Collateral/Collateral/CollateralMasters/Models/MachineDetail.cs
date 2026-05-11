namespace Collateral.CollateralMasters.Models;

public class MachineDetail
{
    public Guid CollateralMasterId { get; private set; }

    // Dedup key — tier 1 (preferred, nullable)
    public string? MachineRegistrationNo { get; private set; }

    // Dedup key — tier 2 (fallback when tier 1 is NULL)
    public string? SerialNo { get; private set; }
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public string? Manufacturer { get; private set; }

    // Appraisal summary (owned)
    public AppraisalSummary AppraisalSummary { get; private set; } = null!;

    // Synced from CollateralMaster for filtered unique index support
    public bool IsDeleted { get; private set; }

    private MachineDetail() { }

    internal MachineDetail(
        Guid collateralMasterId,
        string? machineRegistrationNo,
        string? serialNo,
        string? brand,
        string? model,
        string? manufacturer,
        bool isDeleted)
    {
        CollateralMasterId = collateralMasterId;
        MachineRegistrationNo = machineRegistrationNo;
        SerialNo = serialNo;
        Brand = brand;
        Model = model;
        Manufacturer = manufacturer;
        AppraisalSummary = new AppraisalSummary(null, null, null);
        IsDeleted = isDeleted;
    }

    public void UpdateAppraisalSummary(
        Guid appraisalId,
        string appraisalNumber,
        DateTime appraisedDate)
    {
        AppraisalSummary.Update(appraisalId, appraisalNumber, appraisedDate);
    }

    public void PromoteToRegistration(string machineRegistrationNo)
    {
        MachineRegistrationNo = machineRegistrationNo;
    }

    internal void SetIsDeleted(bool isDeleted) => IsDeleted = isDeleted;

    internal void ApplyAdminEdit(MachineAdminEdit edit, System.Collections.Generic.Dictionary<string, object?> diff)
    {
        if (edit.MachineRegistrationNo is not null && edit.MachineRegistrationNo != MachineRegistrationNo)
        {
            diff["Machine.MachineRegistrationNo"] = new { from = MachineRegistrationNo, to = edit.MachineRegistrationNo };
            MachineRegistrationNo = edit.MachineRegistrationNo;
        }
        if (edit.SerialNo is not null && edit.SerialNo != SerialNo)
        {
            diff["Machine.SerialNo"] = new { from = SerialNo, to = edit.SerialNo };
            SerialNo = edit.SerialNo;
        }
        if (edit.Brand is not null && edit.Brand != Brand)
        {
            diff["Machine.Brand"] = new { from = Brand, to = edit.Brand };
            Brand = edit.Brand;
        }
        if (edit.Model is not null && edit.Model != Model)
        {
            diff["Machine.Model"] = new { from = Model, to = edit.Model };
            Model = edit.Model;
        }
        if (edit.Manufacturer is not null && edit.Manufacturer != Manufacturer)
        {
            diff["Machine.Manufacturer"] = new { from = Manufacturer, to = edit.Manufacturer };
            Manufacturer = edit.Manufacturer;
        }
    }
}
