namespace Integration.Application.Services;

/// <summary>
/// Maps our internal PMA data (read via <see cref="IAppraisalLookupService.GetPmaUpdateDataAsync"/>)
/// to the external LOS update payload shape(s). Land properties produce one payload per title
/// (prices repeated per title, per the LOS spec); a condo property produces exactly one payload.
/// Mapping: forcedSalePrice→forceSellingPrice, buildingInsurancePrice→buildingInsurance,
/// landNumber(LandParcelNumber)→landNo, surveyNumber→surveyNo, titleNumber→titleDeedNo,
/// areaSquareWa→wa, subDistrictName/districtName/provinceName→subDistrict/district/province.
/// </summary>
public static class LosPmaPayloadMapper
{
    // TODO(LOS): confirm action semantics (create-vs-update?) — hardcoded per instruction pending
    // clarification from the LOS spec sheet.
    private const string DefaultAction = "1";

    public static IReadOnlyList<LosPmaUpdatePayload> Map(PmaUpdateData data)
    {
        var prices = new LosPmaPrices(data.SellingPrice, data.ForcedSalePrice, data.BuildingInsurancePrice);

        if (data.LandTitles.Count > 0)
        {
            // The PMA screen is single-title for the whole property (FE reads Titles[0]; the save
            // collapses to one title). Send only the FIRST land title — one LOS call per property.
            return data.LandTitles
                .Take(1)
                .Select(title => new LosPmaUpdatePayload(
                    CasReportNo: data.AppraisalNumber!,
                    LoanApplicationNo: data.LoanApplicationNo,
                    Action: DefaultAction,
                    PmaDetails: new LosPmaDetails(
                        prices,
                        new LosLandCollateral(
                            Rawang: title.Rawang,
                            LandNo: title.LandParcelNumber,
                            SurveyNo: title.SurveyNumber,
                            TitleDeedNo: title.TitleNumber,
                            BookNo: title.BookNumber,
                            PageNo: title.PageNumber,
                            Rai: title.Rai,
                            Ngan: title.Ngan,
                            Wa: title.SquareWa,
                            SubDistrict: title.SubDistrict,
                            District: title.District,
                            Province: title.Province))))
                .ToList();
        }

        if (data.Condo is not null)
        {
            var condo = data.Condo;

            return
            [
                new LosPmaUpdatePayload(
                    CasReportNo: data.AppraisalNumber!,
                    LoanApplicationNo: data.LoanApplicationNo,
                    Action: DefaultAction,
                    PmaDetails: new LosPmaDetails(
                        prices,
                        new LosCondoCollateral(
                            TitleDeedNo: condo.BuiltOnTitleNumber, // sent like the land collateral's titleDeedNo
                            RoomNo: condo.RoomNumber,
                            // LOS floorNo is decimal? but our FloorNumber is free text — parse when
                            // numeric, else null.
                            FloorNo: decimal.TryParse(condo.FloorNumber, out var floorNo) ? floorNo : null,
                            BuildingNo: condo.BuildingNumber, // LOS spec typo "buildigNo" fixed → buildingNo
                            CondoName: condo.CondoName,
                            CondoRegNo: condo.CondoRegistrationNumber,
                            // TODO(LOS): usageArea + owner not captured in the PMA condo data — sent null.
                            UsageArea: null,
                            Owner: null,
                            SubDistrict: condo.SubDistrict,
                            District: condo.District,
                            Province: condo.Province)))
            ];
        }

        return [];
    }
}
