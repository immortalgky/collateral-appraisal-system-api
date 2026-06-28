using System.Data;
using System.Data.Common;
using Shared.Data;

namespace Reporting.Tests.Infrastructure;

/// <summary>
/// Fake ISqlConnectionFactory for testing Dapper-based services.
/// Each call to GetOpenConnection() returns a FakeDbConnection that, when Dapper
/// creates a command on it, dequeues the next pre-configured DataTable from the shared queue.
///
/// Enqueue results in the same order the production code fires SQL queries:
///   1. connectionFactory.QueryAsync(legs SQL)              → EnqueueResult(legsTable)
///   2. connectionFactory.QueryAsync(appointment date SQL)  → EnqueueResult(appointmentTable)
///   3. connectionFactory.QueryAsync(vendor budget)         → EnqueueResult(vendorBudgetTable)
///   4. connectionFactory.QueryAsync(bank budget)           → EnqueueResult(bankBudgetTable)
/// </summary>
public sealed class FakeSqlConnectionFactory : ISqlConnectionFactory
{
    private readonly Queue<DataTable> _resultQueue = new();

    /// <summary>Enqueue a DataTable as the result of the next SQL query call.</summary>
    public void EnqueueResult(DataTable table) => _resultQueue.Enqueue(table);

    public IDbConnection GetOpenConnection() => new FakeDbConnection(_resultQueue);

    public IDbConnection CreateNewConnection() => GetOpenConnection();

    public string GetConnectionString() => "fake";
}

/// <summary>
/// A DbConnection that creates one FakeDbCommand per CreateDbCommand() call.
/// Each command dequeues one DataTable from the shared queue.
/// </summary>
internal sealed class FakeDbConnection(Queue<DataTable> results) : DbConnection
{
    public override string ConnectionString { get; set; } = "fake";
    public override string Database => "fake";
    public override string DataSource => "fake";
    public override string ServerVersion => "fake";
    public override ConnectionState State => ConnectionState.Open;

    public override void Open() { }
    public override void Close() { }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => throw new NotSupportedException("FakeDbConnection does not support transactions.");

    public override void ChangeDatabase(string databaseName)
        => throw new NotSupportedException("FakeDbConnection does not support ChangeDatabase.");

    protected override DbCommand CreateDbCommand()
        => new FakeDbCommand(results.TryDequeue(out var dt) ? dt : new DataTable());
}

/// <summary>
/// A DbCommand backed by a pre-loaded DataTable. ExecuteDbDataReaderAsync returns
/// a DataTableReader over that table. ExecuteScalar returns the value in row 0, col 0
/// (used by Dapper for scalar queries — not the path exercised by QueryAsync).
/// </summary>
internal sealed class FakeDbCommand(DataTable results) : DbCommand
{
    private readonly FakeDbParameterCollection _params = new();

    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => _params;
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    public override int ExecuteNonQuery() => 0;

    public override object? ExecuteScalar()
        => results.Rows.Count > 0 ? results.Rows[0][0] : null;

    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        => results.CreateDataReader();

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
        CommandBehavior behavior, CancellationToken cancellationToken)
        => Task.FromResult<DbDataReader>(results.CreateDataReader());
}

internal sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; }
    public override bool IsNullable { get; set; }
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }
    public override void ResetDbType() { }
}

internal sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _items = new();

    public override int Count => _items.Count;
    public override object SyncRoot => _items;

    public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
    public override void AddRange(Array values) { foreach (var v in values) Add(v); }
    public override void Clear() => _items.Clear();
    public override bool Contains(object value) => _items.Contains((DbParameter)value);
    public override bool Contains(string value) => _items.Any(p => p.ParameterName == value);
    public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);
    public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _items.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _items.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _items.RemoveAll(p => p.ParameterName == parameterName);
    protected override DbParameter GetParameter(int index) => _items[index];
    protected override DbParameter GetParameter(string parameterName) => _items.First(p => p.ParameterName == parameterName);
    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var idx = IndexOf(parameterName);
        if (idx >= 0) _items[idx] = value;
    }
}
