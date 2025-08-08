namespace Request.Requests.Models;

public class RequestComment : Entity<long>
{
    public string Comment { get; private set; }

    private RequestComment(string comment)
    {
        Comment = comment;
    }

    public static RequestComment Create(string comment)
    {
        return new RequestComment(comment);
    }

    public void Update(string comment)
    {
        Comment = comment;
    }
}