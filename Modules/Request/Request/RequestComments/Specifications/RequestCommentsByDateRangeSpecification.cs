namespace Request.RequestComments.Specifications;

public class RequestCommentsByDateRangeSpecification : Specification<RequestComment>
{
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;

    public RequestCommentsByDateRangeSpecification(DateTime startDate, DateTime endDate)
    {
        _startDate = startDate;
        _endDate = endDate;
    }

    public override Expression<Func<RequestComment, bool>> ToExpression()
    {
        return comment => comment.CreatedOn >= _startDate && comment.CreatedOn <= _endDate;
    }
}

public class RecentRequestCommentsSpecification : Specification<RequestComment>
{
    private readonly int _days;

    public RecentRequestCommentsSpecification(int days = 7)
    {
        _days = days;
    }

    public override Expression<Func<RequestComment, bool>> ToExpression()
    {
        var cutoffDate = DateTime.Now.AddDays(-_days);
        return comment => comment.CreatedOn >= cutoffDate;
    }
}