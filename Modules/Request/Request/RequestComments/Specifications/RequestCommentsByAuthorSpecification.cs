namespace Request.RequestComments.Specifications;

public class RequestCommentsByAuthorSpecification : Specification<RequestComment>
{
    private readonly string _author;

    public RequestCommentsByAuthorSpecification(string author)
    {
        _author = author;
    }

    public override Expression<Func<RequestComment, bool>> ToExpression()
    {
        return comment => comment.CreatedBy == _author;
    }
}