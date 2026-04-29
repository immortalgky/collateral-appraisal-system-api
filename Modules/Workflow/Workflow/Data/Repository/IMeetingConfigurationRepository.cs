namespace Workflow.Data.Repository;

public record MeetingDefaults(
    string TitleTemplate,
    string Location,
    string AgendaFromText,
    string AgendaToText);

public interface IMeetingConfigurationRepository
{
    Task<MeetingDefaults> GetMeetingDefaultsAsync(CancellationToken ct = default);
}
