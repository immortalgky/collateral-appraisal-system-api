using Auth.Application.Configurations;
using Shared.Data;

namespace Auth.Infrastructure;

/// <summary>
/// Unit of Work for the Auth module. The generic <see cref="UnitOfWork{TContext}"/> over
/// <see cref="AuthDbContext"/> is sufficient — Auth has no running-number generation, so no override.
/// </summary>
public class AuthUnitOfWork(AuthDbContext context, IServiceProvider serviceProvider)
    : UnitOfWork<AuthDbContext>(context, serviceProvider), IAuthUnitOfWork;
