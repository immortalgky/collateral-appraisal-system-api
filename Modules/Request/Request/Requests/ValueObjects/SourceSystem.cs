using System;

namespace Request.Requests.ValueObjects;

public class SourceSystem
{
    public string? Channel { get; }
    public DateTime? RequestDate { get; }
    public string? RequestBy { get; }
    public string? RequestByName { get; }
    public DateTime? CreatedDate { get; }
    public string? Creator { get; }
    public string? CreatorName { get; }


    private SourceSystem()
    {
        //EF Core
    }

    private SourceSystem(string channel, DateTime? requestDate, string requestBy, string reqeustByName,
        DateTime? createDate, string creator, string creatorName)
    {
        Channel = channel;
        RequestDate = requestDate;
        RequestBy = requestBy;
        RequestByName = reqeustByName;
        CreatedDate = createDate;
        Creator = creator;
        CreatorName = creatorName;
    }

    public static SourceSystem Create(string channel, DateTime? requestDate, string requestBy, string reqeustByName,
        DateTime? createDate,
        string creator, string creatorName)
    {
        return new SourceSystem(channel, requestDate, requestBy, reqeustByName, createDate, creator, creatorName);
    }
}
