using System.Text;
using System.Text.RegularExpressions;
using Reporting.Application.OperationalReports.Rcas001;
using Reporting.Application.OperationalReports.Rcas002;
using Reporting.Application.OperationalReports.Rcas004;
using Reporting.Application.OperationalReports.Rcas008;
using Reporting.Application.OperationalReports.Rcas009;

namespace Reporting.Tests;

/// <summary>
/// The RCAS rows are positional records read with SELECT * from their view. Dapper matches constructor parameters to
/// result columns BY POSITION, so one column added to a view without the record (RCAS008/009 both lost
/// AppraisalCompanyEn that way) fails every request with "A parameterless default constructor or one matching
/// signature is required". This pins each view's output column list, read from the view script, to its record.
/// </summary>
public class RcasViewRecordDriftTests
{
    public static TheoryData<string, Type> ViewsAndRecords => new()
    {
        { "vw_RCAS001_AppraisalBooks", typeof(Rcas001Row) },
        { "vw_RCAS002_ReappraisalDue", typeof(Rcas002Row) },
        { "vw_RCAS004_ConstructionInspection", typeof(Rcas004Row) },
        { "vw_RCAS008_ServiceQuality", typeof(Rcas008Row) },
        { "vw_RCAS009_FeeSummary", typeof(Rcas009Row) },
    };

    [Theory]
    [MemberData(nameof(ViewsAndRecords))]
    public void Record_parameters_match_the_view_columns_in_order(string view, Type record)
    {
        var columns = OutputColumns(File.ReadAllText(ViewScript(view)));
        var parameters = record.GetConstructors().Single().GetParameters().Select(p => p.Name!).ToList();

        Assert.Equal(columns, parameters);
    }

    [Fact]
    public void Parser_reads_the_select_list_of_a_cte_view_and_skips_comments_strings_and_subqueries()
    {
        const string sql = """
            CREATE OR ALTER VIEW x.v AS
            WITH c AS (SELECT 1 AS Hidden, 2 AS Hidden2 FROM t)
            SELECT c.Id,                       -- a, comment
                   COALESCE(a, 'x, y') AS Name, /* b, c */
                   (SELECT TOP 1 d FROM u WHERE u.k = c.k) AS Sub,
                   CASE WHEN a = 1 THEN 'FROM' ELSE 'z' END AS [Flag],
                   c.Last
            FROM c JOIN d ON d.id = c.id
            """;

        Assert.Equal(["Id", "Name", "Sub", "Flag", "Last"], OutputColumns(sql));
    }

    private static string ViewScript(string view)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "Database", "Scripts", "Views", "Reporting", view + ".sql");
            if (File.Exists(path)) return path;
        }

        throw new FileNotFoundException($"View script {view}.sql not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>
    /// Output column names of the view's main SELECT, in order: the first SELECT outside any parentheses (a CTE body
    /// is inside them) up to the first FROM at the same depth, split on top-level commas. The name is the alias
    /// after the last AS, else the last identifier of the expression (<c>v.Id</c> gives Id).
    /// </summary>
    private static List<string> OutputColumns(string sql)
    {
        var text = Strip(sql);
        var depth = 0;
        int? start = null;
        var items = new List<string>();
        var item = new StringBuilder();
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (start is not null) item.Append(ch);
                if (ch == '\'') inString = false;
                continue;
            }

            if (ch == '\'')
            {
                inString = true;
                if (start is not null) item.Append(ch);
                continue;
            }

            if (ch == '(') depth++;
            else if (ch == ')') depth--;

            if (depth == 0 && start is null && IsWord(text, i, "SELECT"))
            {
                start = i + "SELECT".Length;
                i = start.Value - 1;
                continue;
            }

            if (start is not null)
            {
                if (depth == 0 && IsWord(text, i, "FROM"))
                {
                    items.Add(item.ToString());
                    break;
                }

                if (depth == 0 && ch == ',')
                {
                    items.Add(item.ToString());
                    item.Clear();
                    continue;
                }

                item.Append(ch);
            }
        }

        return items.Select(Name).ToList();
    }

    private static string Name(string expression)
    {
        var e = expression.Trim();
        var alias = Regex.Match(e, @"\bAS\s+\[?(\w+)\]?\s*$", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
        if (alias.Success) return alias.Groups[1].Value;
        var last = Regex.Match(e, @"(\w+)\]?\s*$");
        return last.Groups[1].Value;
    }

    private static bool IsWord(string text, int i, string word) =>
        string.Compare(text, i, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0
        && (i == 0 || !char.IsLetterOrDigit(text[i - 1]) && text[i - 1] != '_')
        && (i + word.Length >= text.Length || !char.IsLetterOrDigit(text[i + word.Length]) && text[i + word.Length] != '_');

    /// <summary>Removes <c>--</c> and <c>/* */</c> comments, leaving string literals alone.</summary>
    private static string Strip(string sql)
    {
        var sb = new StringBuilder();
        var inString = false;
        for (var i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];
            if (inString)
            {
                sb.Append(ch);
                if (ch == '\'') inString = false;
            }
            else if (ch == '\'')
            {
                inString = true;
                sb.Append(ch);
            }
            else if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n') i++;
                sb.Append('\n');
            }
            else if (ch == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                i++;
            }
            else sb.Append(ch);
        }

        return sb.ToString();
    }
}
