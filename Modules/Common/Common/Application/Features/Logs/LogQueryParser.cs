using System.Text.RegularExpressions;
using Dapper;

namespace Common.Application.Features.Logs;

/// <summary>
/// Parses the `q` search box into a parameterized SQL WHERE fragment. Pure and static — no DB
/// access, so it's covered by plain unit tests (see LogQueryParserTests). Every value is bound
/// as a Dapper parameter; nothing here is string-concatenated into the SQL text.
///
/// Supported syntax (see also the FE "search guide" drawer):
///   word / "a phrase"      free text over Message and Exception (all terms ANDed)
///   -word / -"a phrase"    negated free text
///   appraisal:VALUE        AppraisalId, or a lookup by AppraisalNumber when VALUE isn't a GUID
///   request:/corr:/user:/level:VALUE   exact match on the matching column
///   path:/source:VALUE     contains match on RequestPath / SourceContext (falls back to the JSON
///                          Properties blob for rows written before these columns existed)
///   a bare GUID            OR across every ID column
///   unknown key:VALUE      treated as a plain free-text term, colon and all
/// </summary>
public static class LogQueryParser
{
    // Won't fix: key:"quoted value" (a spaced value after a key:) isn't supported — the quote only
    // starts a phrase token at the very start of a token, so `user:"john doe"` splits into two plain
    // free-text tokens instead of one key clause. Not worth the added complexity: the FE never
    // generates this syntax, and typing a space into a key value degrades to a free-text search
    // rather than erroring, which is an acceptable result for a search box.
    private static readonly Regex TokenPattern = new(@"-?""[^""]*""|-?\S+", RegexOptions.Compiled);

    private static readonly string[] KnownKeys =
        ["appraisal", "request", "corr", "user", "level", "path", "source"];

    // HasResidualPredicate is true when the clause includes at least one non-sargable term (free
    // text, or path:/source: contains) — a plain scan/filter over the whole table, index or not.
    // Callers use this to decide whether the Id-range-bounding trick (see LogIdBounding) is worth
    // the extra round trip: it only helps once a residual predicate is in play.
    //
    // HasLevelKey is tracked separately, not folded into HasResidualPredicate, because it needs
    // bounding for a different reason: Level = 'X' is perfectly sargable on its own (seeks
    // IX_Logs_Level_TimeStamp), but ORDER BY Id DESC + TOP(N) can make the optimizer sort-avoid its
    // way into a Clustered Index Scan filtering Level residually while walking Id order instead —
    // measured 155,518 reads for a selective level over a recent 24h window with no bounding, same
    // pathology as a rare free-text term (see SearchLogs' `levels` filter, which hits this too).
    // corr:/request:/user:/appraisal:/a bare GUID don't need this: they each seek their own
    // single-column filtered index (IX_Logs_CorrelationId etc., or IX_Logs_UserName_TimeStamp),
    // and being highly selective, that stays cheap (measured single-digit logical reads) regardless
    // of which plan the optimizer picks.
    public record Result(string? WhereClause, DynamicParameters Parameters, bool HasResidualPredicate, bool HasLevelKey);

    public static Result Parse(string? query)
    {
        var parameters = new DynamicParameters();
        if (string.IsNullOrWhiteSpace(query))
            return new Result(null, parameters, false, false);

        var clauses = new List<string>();
        var paramIndex = 0;
        var hasResidualPredicate = false;
        var hasLevelKey = false;

        foreach (Match match in TokenPattern.Matches(query))
        {
            var token = match.Value;
            var negate = token.StartsWith('-');
            var body = negate ? token[1..] : token;
            if (body.Length == 0) continue;

            var isPhrase = body.Length >= 2 && body[0] == '"' && body[^1] == '"';
            if (isPhrase)
            {
                var phrase = body[1..^1];
                if (phrase.Length == 0) continue;
                hasResidualPredicate = true;
                clauses.Add(Wrap(BuildFreeTextClause(phrase, parameters, ref paramIndex), negate));
                continue;
            }

            var colonIndex = body.IndexOf(':');
            var key = colonIndex > 0 ? body[..colonIndex].ToLowerInvariant() : null;
            var value = colonIndex > 0 ? body[(colonIndex + 1)..] : null;

            string clause;
            if (key is not null && KnownKeys.Contains(key) && !string.IsNullOrEmpty(value))
            {
                clause = key switch
                {
                    "appraisal" => BuildAppraisalClause(value, parameters, ref paramIndex),
                    "request" => BuildEqualsClause("RequestId", value, parameters, ref paramIndex),
                    "corr" => BuildEqualsClause("CorrelationId", value, parameters, ref paramIndex),
                    "user" => BuildEqualsClause("UserName", value, parameters, ref paramIndex),
                    "level" => BuildEqualsClause("Level", value, parameters, ref paramIndex),
                    "path" => BuildContainsClause(
                        "COALESCE(RequestPath, JSON_VALUE(Properties,'$.Properties.RequestPath'))",
                        value, parameters, ref paramIndex),
                    "source" => BuildContainsClause(
                        "COALESCE(SourceContext, JSON_VALUE(Properties,'$.Properties.SourceContext'))",
                        value, parameters, ref paramIndex),
                    _ => throw new InvalidOperationException($"Unhandled known key '{key}'.")
                };
                if (key is "path" or "source") hasResidualPredicate = true;
                if (key is "level") hasLevelKey = true;
            }
            else if (Guid.TryParse(body, out _))
            {
                clause = BuildGuidOrClause(body, parameters, ref paramIndex);
            }
            else
            {
                hasResidualPredicate = true;
                clause = BuildFreeTextClause(body, parameters, ref paramIndex);
            }

            clauses.Add(Wrap(clause, negate));
        }

        return new Result(clauses.Count > 0 ? string.Join(" AND ", clauses) : null, parameters, hasResidualPredicate, hasLevelKey);
    }

    // Not a plain NOT(): three-valued SQL logic means NOT(UNKNOWN) is still UNKNOWN, not TRUE, so
    // a straight NOT() silently drops rows where the clause can't be evaluated at all — e.g. a
    // negated free-text search excluding every row with a NULL Message AND a NULL Exception, or
    // -user:/-path:/-source: excluding every row where that column is NULL. Folding through CASE
    // collapses UNKNOWN to 0 (i.e. "did not match"), so negation correctly includes those rows.
    private static string Wrap(string clause, bool negate) =>
        negate ? $"(CASE WHEN {clause} THEN 1 ELSE 0 END) = 0" : clause;

    private static string NextParam(ref int paramIndex) => "q" + paramIndex++;

    private static string BuildFreeTextClause(string text, DynamicParameters parameters, ref int paramIndex)
    {
        var p = NextParam(ref paramIndex);
        parameters.Add(p, "%" + EscapeLike(text) + "%");
        return $"(Message LIKE @{p} ESCAPE '\\' OR Exception LIKE @{p} ESCAPE '\\')";
    }

    private static string BuildEqualsClause(string column, string value, DynamicParameters parameters, ref int paramIndex)
    {
        var p = NextParam(ref paramIndex);
        parameters.Add(p, value.Trim());
        return $"{column} = @{p}";
    }

    private static string BuildContainsClause(string sqlExpression, string value, DynamicParameters parameters, ref int paramIndex)
    {
        var p = NextParam(ref paramIndex);
        parameters.Add(p, "%" + EscapeLike(value) + "%");
        return $"{sqlExpression} LIKE @{p} ESCAPE '\\'";
    }

    private static string BuildGuidOrClause(string guidText, DynamicParameters parameters, ref int paramIndex)
    {
        var p = NextParam(ref paramIndex);
        parameters.Add(p, guidText);
        string[] columns =
            ["CorrelationId", "EntityId", "AppraisalId", "RequestId", "WorkflowInstanceId", "CollateralId", "DocumentId"];
        return "(" + string.Join(" OR ", columns.Select(c => $"{c} = @{p}")) + ")";
    }

    private static string BuildAppraisalClause(string value, DynamicParameters parameters, ref int paramIndex)
    {
        if (Guid.TryParse(value, out _))
            return BuildEqualsClause("AppraisalId", value, parameters, ref paramIndex);

        var p = NextParam(ref paramIndex);
        parameters.Add(p, value.Trim());
        return $"AppraisalId IN (SELECT CAST(Id AS nvarchar(64)) FROM appraisal.Appraisals WHERE AppraisalNumber = @{p})";
    }

    // Escapes SQL Server LIKE wildcards so user input matches literally; paired with ESCAPE '\'.
    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
