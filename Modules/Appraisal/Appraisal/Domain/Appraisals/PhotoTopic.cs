namespace Appraisal.Domain.Appraisals;

public class PhotoTopic : Entity<Guid>
{
    public Guid AppraisalId { get; private set; }
    public string TopicName { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public int DisplayColumns { get; private set; } = 1;

    private PhotoTopic()
    {
    }

    public static PhotoTopic Create(
        Guid appraisalId,
        string topicName,
        int sortOrder,
        int displayColumns = 1)
    {
        return new PhotoTopic
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            TopicName = topicName,
            SortOrder = sortOrder,
            DisplayColumns = displayColumns
        };
    }

    public void Update(string topicName, int sortOrder, int displayColumns)
    {
        TopicName = topicName;
        SortOrder = sortOrder;
        DisplayColumns = displayColumns;
    }
}
