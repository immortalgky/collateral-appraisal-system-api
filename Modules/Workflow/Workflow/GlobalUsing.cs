// System namespaces

global using System.Reflection;
global using System.Text.Json;

// Microsoft namespaces
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

// Third-party packages
global using Carter;
global using MassTransit;
global using MediatR;

// Shared namespaces
global using Shared.CQRS;
global using Shared.Data.Extensions;
global using Shared.DDD;
global using Shared.Exceptions;
global using Shared.Time;

// Workflow module namespaces (organized by feature)
global using Workflow.AssigneeSelection.Core;
global using Workflow.AssigneeSelection.Factories;
global using Workflow.AssigneeSelection.Strategies;
global using Workflow.Data;
global using Workflow.Data.Repository;
global using Workflow.Events;
global using Workflow.Sagas.AppraisalSaga;
global using Workflow.Sagas.Models;
global using Workflow.Services;
global using Workflow.Services.Groups;
global using Workflow.Services.Hashing;
global using Workflow.Tasks.Models;
global using Workflow.Workflow.Resilience;

// Type aliases (should be at the end)
global using TaskStatus = Workflow.Tasks.ValueObjects.TaskStatus;