using Shared.Data;

namespace Auth.Application.Configurations;

/// <summary>
/// Unit of Work marker for the Auth module. Lets commands opt into the shared
/// <c>TransactionalBehavior</c> via <c>ITransactionalCommand&lt;IAuthUnitOfWork&gt;</c> so the whole
/// handler (including UserManager writes on the same DbContext) commits or rolls back atomically.
/// </summary>
public interface IAuthUnitOfWork : IUnitOfWork;
