namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Multiple title deeds per land property (adjacent plots grouped under one land).
/// </summary>
public class LandTitle : Entity<Guid>
{
    public Guid LandAppraisalDetailId { get; private set; }

    // Title Deed Info
    public string TitleNumber { get; private set; } = default!;
    public string TitleType { get; private set; } = default!;
    public string? BookNumber { get; private set; }
    public string? PageNumber { get; private set; }
    public string? LandParcelNumber { get; private set; }
    public string? SurveyNumber { get; private set; }
    public string? MapSheetNumber { get; private set; }
    public string? Rawang { get; private set; }
    public string? AerialMapName { get; private set; }
    public string? AerialMapNumber { get; private set; }

    // Area (Thai units)
    public LandArea? Area { get; private set; }

    // Boundary & Validation
    public string? BoundaryMarkerType { get; private set; }
    public string? BoundaryMarkerRemark { get; private set; }
    public string? DocumentValidationResultType { get; private set; }
    public bool? IsMissingFromSurvey { get; private set; }

    // Pricing
    public decimal? GovernmentPricePerSqWa { get; private set; }
    public decimal? GovernmentPrice { get; private set; }

    // Remarks
    public string? Remark { get; private set; }

    private LandTitle()
    {
        // For EF Core
    }

    public static LandTitle Create(
        Guid landAppraisalDetailId,
        string titleDeedNumber,
        string titleDeedType)
    {
        return new LandTitle
        {
            LandAppraisalDetailId = landAppraisalDetailId,
            TitleNumber = titleDeedNumber,
            TitleType = titleDeedType
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        string? bookNumber,
        string? pageNumber,
        string? landParcelNumber,
        string? surveyNumber,
        string? mapSheetNumber,
        string? rawang,
        string? aerialMapName,
        string? aerialMapNumber,
        LandArea? area,
        string? boundaryMarkerType,
        string? boundaryMarkerRemark,
        string? documentValidationResultType,
        bool? isMissingFromSurvey,
        decimal? governmentPricePerSqWa,
        decimal? governmentPrice,
        string? remark
    )
    {
        BookNumber = bookNumber;
        PageNumber = pageNumber;
        LandParcelNumber = landParcelNumber;
        SurveyNumber = surveyNumber;
        MapSheetNumber = mapSheetNumber;
        Rawang = rawang;
        AerialMapName = aerialMapName;
        AerialMapNumber = aerialMapNumber;
        Area = area;
        BoundaryMarkerType = boundaryMarkerType;
        BoundaryMarkerRemark = boundaryMarkerRemark;
        DocumentValidationResultType = documentValidationResultType;
        IsMissingFromSurvey = isMissingFromSurvey;
        GovernmentPricePerSqWa = governmentPricePerSqWa;
        GovernmentPrice = governmentPrice;
        Remark = remark;
    }

    /// <summary>
    /// Applies admin corrections to this title, recording each change in <paramref name="diff"/>.
    ///
    /// Reaches what <see cref="Update"/> cannot: <see cref="TitleNumber"/> and <see cref="TitleType"/>
    /// have no mutator anywhere else, so a mistyped title number was previously uncorrectable. Both
    /// are non-nullable, so a blank incoming value means "not supplied" rather than "clear".
    /// </summary>
    internal void ApplyCorrection(LandTitleCorrection edit, Dictionary<string, object?> diff)
    {
        var prefix = $"Land.Title[{Id}]";

        if (!string.IsNullOrWhiteSpace(edit.TitleNumber))
            CorrectionDiff.Apply($"{prefix}.TitleNumber", TitleNumber, edit.TitleNumber, v => TitleNumber = v!, diff);
        if (!string.IsNullOrWhiteSpace(edit.TitleType))
            CorrectionDiff.Apply($"{prefix}.TitleType", TitleType, edit.TitleType, v => TitleType = v!, diff);
        CorrectionDiff.Apply($"{prefix}.BookNumber", BookNumber, edit.BookNumber, v => BookNumber = v, diff);
        CorrectionDiff.Apply($"{prefix}.PageNumber", PageNumber, edit.PageNumber, v => PageNumber = v, diff);
        CorrectionDiff.Apply($"{prefix}.LandParcelNumber", LandParcelNumber, edit.LandParcelNumber, v => LandParcelNumber = v, diff);
        CorrectionDiff.Apply($"{prefix}.SurveyNumber", SurveyNumber, edit.SurveyNumber, v => SurveyNumber = v, diff);
        CorrectionDiff.Apply($"{prefix}.MapSheetNumber", MapSheetNumber, edit.MapSheetNumber, v => MapSheetNumber = v, diff);
        CorrectionDiff.Apply($"{prefix}.Rawang", Rawang, edit.Rawang, v => Rawang = v, diff);
        CorrectionDiff.Apply($"{prefix}.AerialMapName", AerialMapName, edit.AerialMapName, v => AerialMapName = v, diff);
        CorrectionDiff.Apply($"{prefix}.AerialMapNumber", AerialMapNumber, edit.AerialMapNumber, v => AerialMapNumber = v, diff);
        // Area (Rai/Ngan/SquareWa) is deliberately NOT correctable: every valuation on the
        // appraisal is computed from it, and this feature corrects descriptive data only — it
        // does not recompute prices or send the appraisal back through the workflow. Changing
        // the area here would leave the recorded values disagreeing with their own inputs.
        CorrectionDiff.Apply($"{prefix}.BoundaryMarkerType", BoundaryMarkerType, edit.BoundaryMarkerType, v => BoundaryMarkerType = v, diff);
        CorrectionDiff.Apply($"{prefix}.BoundaryMarkerRemark", BoundaryMarkerRemark, edit.BoundaryMarkerRemark, v => BoundaryMarkerRemark = v, diff);
        CorrectionDiff.Apply($"{prefix}.DocumentValidationResultType", DocumentValidationResultType, edit.DocumentValidationResultType, v => DocumentValidationResultType = v, diff);
        CorrectionDiff.Apply($"{prefix}.IsMissingFromSurvey", IsMissingFromSurvey, edit.IsMissingFromSurvey, v => IsMissingFromSurvey = v, diff);
        CorrectionDiff.Apply($"{prefix}.GovernmentPricePerSqWa", GovernmentPricePerSqWa, edit.GovernmentPricePerSqWa, v => GovernmentPricePerSqWa = v, diff);
        CorrectionDiff.Apply($"{prefix}.GovernmentPrice", GovernmentPrice, edit.GovernmentPrice, v => GovernmentPrice = v, diff);
        CorrectionDiff.Apply($"{prefix}.Remark", Remark, edit.Remark, v => Remark = v, diff);
    }
}
