using Shared.Data;

namespace Integration.Infrastructure;

public interface IIntegrationUnitOfWork : IUnitOfWork;

public class IntegrationUnitOfWork(IntegrationDbContext dbContext, IServiceProvider sp)
    : UnitOfWork<IntegrationDbContext>(dbContext, sp), IIntegrationUnitOfWork;
