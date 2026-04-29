using Workflow.Meetings.Domain;

namespace Workflow.Data.Repository;

public class MeetingConfigurationRepository(WorkflowDbContext dbContext) : IMeetingConfigurationRepository
{
    // Hard-coded fallbacks mirror the seed script values — keeps the system operational
    // even if a row is accidentally deleted from the table.
    private const string FallbackTitleTemplate  = "ขออนุมัติราคาประเมิน ครั้งที่ {meetingNo}";
    private const string FallbackLocation       = "ห้องประชุม 32/1";
    private const string FallbackAgendaFromText = "เลขานุการคณะกรรมการฯ";
    private const string FallbackAgendaToText   = "คณะกรรมการกำหนดราคาประเมินหลักประกันและทรัพย์สิน";

    public async Task<MeetingDefaults> GetMeetingDefaultsAsync(CancellationToken ct = default)
    {
        var dict = await dbContext.MeetingConfigurations
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);

        return new MeetingDefaults(
            TitleTemplate:  dict.GetValueOrDefault(MeetingConfigurationKeys.TitleTemplate,  FallbackTitleTemplate),
            Location:       dict.GetValueOrDefault(MeetingConfigurationKeys.Location,       FallbackLocation),
            AgendaFromText: dict.GetValueOrDefault(MeetingConfigurationKeys.AgendaFromText, FallbackAgendaFromText),
            AgendaToText:   dict.GetValueOrDefault(MeetingConfigurationKeys.AgendaToText,   FallbackAgendaToText));
    }
}
