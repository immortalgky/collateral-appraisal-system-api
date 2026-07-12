namespace Workflow.Contracts.Sql;

public static class PreviousMeetingNoQuery
{
    /// <summary>
    /// SELECT for the previous meeting's number = highest-numbered meeting (by BE year + seq)
    /// below the current meeting's number, excluding Cancelled and New meetings.
    /// <paramref name="idParam"/> is the SQL parameter name holding the current meeting Id
    /// (without '@'), e.g. "Id" or "MeetingId".
    /// </summary>
    public static string Sql(string idParam) => $"""
        SELECT TOP 1 prev.MeetingNo
        FROM workflow.Meetings curr
        INNER JOIN workflow.Meetings prev
            ON prev.Id <> curr.Id
            AND prev.Status NOT IN ('Cancelled', 'New')
            AND ( prev.MeetingNoYear <  curr.MeetingNoYear
               OR (prev.MeetingNoYear = curr.MeetingNoYear AND prev.MeetingNoSeq < curr.MeetingNoSeq) )
        WHERE curr.Id = @{idParam}
        ORDER BY prev.MeetingNoYear DESC, prev.MeetingNoSeq DESC
        """;
}
