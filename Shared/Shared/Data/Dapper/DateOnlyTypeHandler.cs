using System.Data;
using Dapper;

namespace Shared.Data.Dapper;

/// <summary>
/// Dapper materializer for <see cref="DateOnly"/>. SqlClient returns SQL Server <c>date</c> columns as
/// <see cref="DateTime"/>, which Dapper otherwise can't cast to <see cref="DateOnly"/> — and on the parameter
/// side, <see cref="DateOnly"/> isn't a native ADO.NET type, so we send it as <see cref="DateTime"/>.
/// Register once at startup via <c>SqlMapper.AddTypeHandler(new DateOnlyTypeHandler())</c>;
/// the same handler covers <c>DateOnly?</c> automatically.
/// </summary>
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) =>
        value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            DateOnly d => d,
            string s => DateOnly.Parse(s),
            _ => throw new InvalidCastException(
                $"Cannot convert {value?.GetType().Name ?? "null"} to DateOnly"),
        };
}
