namespace Appraisal.AppraisalProperties.ValueObjects;

public class CondoRoomLayout : ValueObject
{
    public string? RoomLayout { get; }
    public string? RoomLayoutOther { get; }

    private CondoRoomLayout(string? roomLayout, string? roomLayoutOther)
    {
        RoomLayout = roomLayout;
        RoomLayoutOther = roomLayoutOther;
    }

    public static CondoRoomLayout Create(string? roomLayout, string? roomLayoutOther)
    {
        return new CondoRoomLayout(roomLayout, roomLayoutOther);
    }
}
