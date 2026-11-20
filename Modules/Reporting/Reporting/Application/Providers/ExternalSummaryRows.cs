using Reporting.Application.Models;

namespace Reporting.Application.Providers;

/// <summary>
/// The รายละเอียดทรัพย์สิน row of the external book's สรุปผลการประเมินมูลค่าทรัพย์สิน page: one block per part of the property that exists
/// (ที่ดิน, สิ่งปลูกสร้าง, ห้องชุด, เครื่องจักร), built from what the letter used to print (the deed summary, the grouped building text, the
/// machine registrations) and the sections the book has loaded.
/// </summary>
internal static class ExternalSummaryRows
{
    public static IReadOnlyList<ExternalPropertyBlock> PropertyBlocks(AppraisalSummaryModel model)
    {
        var blocks = new List<ExternalPropertyBlock>();

        var land = new List<string>();
        if (DeedLine(model) is { } deeds) land.Add(deeds);
        if (model.LandAreaWords is { } area) land.Add("เนื้อที่ดินรวมตามเอกสารสิทธิ์ " + area);
        if (land.Count > 0) blocks.Add(new ExternalPropertyBlock { Title = "ที่ดิน", Lines = land });

        // The set the cover counts: appraised buildings, plus the development items that go with them.
        var buildings = LetterOwnerRows.Counted(model).Count;
        var hasDev = CoverNarrative.HasAppraisedDevelopment(model);
        if (buildings > 0 || hasDev)
        {
            var count = buildings > 0 ? $"จำนวน {buildings} รายการ{(hasDev ? " และส่วนพัฒนาอื่นๆ" : "")}" : "ส่วนพัฒนาอื่นๆ";
            var lines = new List<string> { $"{count} (ดูรายละเอียดตามหัวข้อ {LetterOwnerRows.BuildingSections(model)})" };
            if (model.BuildingDetailsText is { } details) lines.AddRange(details.Split('\n'));
            blocks.Add(new ExternalPropertyBlock { Title = "สิ่งปลูกสร้าง", Lines = lines });
        }

        if (model.CondoSection is { Units.Count: > 0 } condo)
            blocks.Add(new ExternalPropertyBlock
            {
                Title = "ห้องชุด",
                Lines = [$"จำนวน {condo.Units.Count} ห้อง" + (condo.UsableArea is > 0m ? $" เนื้อที่ห้องชุดรวม {condo.UsableArea:#,##0.##} ตารางเมตร" : "")]
            });

        var machines = model.MachineSection?.Machines.Count ?? 0;
        if (machines > 0)
        {
            // The real machine rows, not the registration entries (a run "0001 ถึง 0029" is one entry for 29 machines).
            var lines = new List<string> { $"จำนวน {machines} รายการ" };
            if (model.MachineRegistrations.Count == 1)
                lines.Add("เลขทะเบียน " + model.MachineRegistrations[0]);
            else if (model.MachineRegistrations.Count > 1)
            {
                lines.Add("เลขทะเบียน");
                lines.AddRange(model.MachineRegistrations.Select((r, i) => $"({i + 1}) {r}"));
            }
            blocks.Add(new ExternalPropertyBlock { Title = "เครื่องจักร", Lines = lines });
        }

        return blocks;
    }

    /// <summary>
    /// The ที่ดิน line: the count first, then the numbers grouped by document type — "ประกอบด้วยเอกสารสิทธิ์ จำนวน 4 ฉบับ ได้แก่ โฉนดที่ดินเลขที่ 101, 102
    /// และ 103 และ น.ส.3 ก. เลขที่ 204". Above 10 deeds the groups are a count per type, and the section where the deeds are listed follows. A note for
    /// deeds with no number comes before it. Null when there are no numbered deeds.
    /// </summary>
    public static string? DeedLine(AppraisalSummaryModel model)
    {
        if (model.TitleDeedGroups is not { } numbers) return null;
        var line = $"ประกอบด้วยเอกสารสิทธิ์ จำนวน {model.TotalTitleDeeds} ฉบับ ได้แก่ {numbers}";
        if (model.UnnumberedTitleDeeds > 0) line += $" (ไม่ระบุเลขที่ {model.UnnumberedTitleDeeds} ฉบับ)";
        if (model.TitleDeedsSummarised) line += $" (รายละเอียดตามข้อ {(model.BodyType == "construction" ? "2" : "2.1")})";
        return line;
    }
}
