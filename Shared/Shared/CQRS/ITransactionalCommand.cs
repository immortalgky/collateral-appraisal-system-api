using Shared.Data;

namespace Shared.CQRS;

public interface ITransactionalCommand<TUnitOfWork> where TUnitOfWork : IUnitOfWork;