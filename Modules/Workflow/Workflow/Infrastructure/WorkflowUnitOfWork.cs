using Shared.Data;
using Workflow.Data;

namespace Workflow.Infrastructure;

public class WorkflowUnitOfWork(WorkflowDbContext context, IServiceProvider serviceProvider)
    : UnitOfWork<WorkflowDbContext>(context, serviceProvider), IWorkflowUnitOfWork;
