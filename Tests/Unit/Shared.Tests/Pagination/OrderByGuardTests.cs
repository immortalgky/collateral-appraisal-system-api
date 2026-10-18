using Shared.Exceptions;
using Shared.Pagination;

namespace Shared.Tests.Pagination;

public class OrderByGuardTests
{
    [Theory]
    [InlineData("CreatedAt DESC, Id ASC")]
    [InlineData("COALESCE(a, b) DESC, Id ASC")]
    [InlineData("CASE WHEN Status IN ('Completed', 'Cancelled') THEN 1 ELSE 0 END, SLADueDate ASC, Id ASC")]
    public void Plain_clauses_and_letters_only_literals_are_accepted(string orderBy) =>
        Assert.Null(Record.Exception(() => DapperPaginationExtensions.ValidateOrderBy(orderBy)));

    [Theory]
    [InlineData("Id; DROP TABLE x")]
    [InlineData("Id -- comment")]
    [InlineData("Id /* c */")]
    [InlineData("Id, 'a b'")]                       // not letters only
    [InlineData("Id, 'abc")]                        // unterminated
    [InlineData("Id, 'x' + (SELECT 1); --")]        // a literal does not excuse the rest
    [InlineData("Id, \"Completed\"")]
    [InlineData("Id, 'a''b'")]                      // embedded quote
    [InlineData("Id, 'Completed' ; DROP TABLE x")]
    [InlineData("")]
    public void Anything_else_with_a_quote_terminator_or_comment_is_rejected(string orderBy) =>
        Assert.Throws<BadRequestException>(() => DapperPaginationExtensions.ValidateOrderBy(orderBy));
}
