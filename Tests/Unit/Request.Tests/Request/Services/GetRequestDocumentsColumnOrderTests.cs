using System.Reflection;
using System.Text.RegularExpressions;
using Request.Application.Features.RequestDocuments.GetRequestDocumentsByRequestId;

namespace Request.Tests.Request.Services;

/// <summary>
/// Dapper binds these positional records by column position, not name, so a column added to one SELECT
/// but not the record (or the other way round) shifts every later field without failing. Pins the order.
/// </summary>
public class GetRequestDocumentsColumnOrderTests
{
    private static List<string> SelectColumns(int resultSet)
    {
        var select = Regex.Split(GetRequestDocumentsByRequestIdQueryHandler.Sql, @"(?m)^\s*-- Result set \d+.*$")[resultSet];
        select = Regex.Match(select, @"SELECT(?<cols>.*?)\bFROM\b", RegexOptions.Singleline).Groups["cols"].Value;

        var columns = new List<string>();
        var depth = 0;
        var current = new System.Text.StringBuilder();
        foreach (var ch in select)
        {
            if (ch == '(') depth++;
            if (ch == ')') depth--;
            if (ch == ',' && depth == 0) { columns.Add(current.ToString()); current.Clear(); }
            else current.Append(ch);
        }
        columns.Add(current.ToString());

        // Output name: the alias after AS, else the last dotted part.
        return columns
            .Select(c => Regex.Match(c.Trim(), @"(?:\bAS\s+|\.)\[?(?<n>\w+)\]?\s*$", RegexOptions.IgnoreCase).Groups["n"].Value)
            .ToList();
    }

    private static List<string> ParameterNames(Type record) =>
        record.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(c => c.GetParameters().Length > 1)
            .GetParameters().Select(p => p.Name!).ToList();

    [Fact]
    public void Request_level_select_matches_DocumentItemDto_column_for_column()
    {
        var columns = SelectColumns(1);

        Assert.Equal(ParameterNames(typeof(DocumentItemDto)), columns, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Source", columns[^1]);
    }

    [Fact]
    public void Title_select_matches_TitleDocumentRow_column_for_column()
    {
        var columns = SelectColumns(2);

        Assert.Equal(ParameterNames(typeof(TitleDocumentRow)), columns, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Source", columns[^1]);
    }
}
