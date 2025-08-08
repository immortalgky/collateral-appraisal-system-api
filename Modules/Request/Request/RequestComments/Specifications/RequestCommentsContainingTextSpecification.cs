namespace Request.RequestComments.Specifications;

public class RequestCommentsContainingTextSpecification : Specification<RequestComment>
{
    private readonly string _searchText;

    public RequestCommentsContainingTextSpecification(string searchText)
    {
        _searchText = searchText;
    }

    public override Expression<Func<RequestComment, bool>> ToExpression()
    {
        return comment => comment.Comment.Contains(_searchText);
    }
}