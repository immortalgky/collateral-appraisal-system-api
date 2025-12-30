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
global using Shared.Data.Extensions;
global using Shared.DDD;
global using Shared.Exceptions;
global using Shared.Time;
global using Shared.CQRS;

// Assignment module namespaces (organized by feature)
global using Assignment.Domain.AssigneeSelection.Core;
global using Assignment.Domain.AssigneeSelection.Factories;
global using Assignment.Domain.AssigneeSelection.Strategies;
global using Assignment.Data;
global using Assignment.Data.Repository;
global using Assignment.Domain.Events;
global using Assignment.Domain.Sagas.AppraisalSaga;
global using Assignment.Domain.Sagas.Models;
global using Assignment.Services;
global using Assignment.Services.Groups;
global using Assignment.Services.Hashing;
global using Assignment.Domain.Tasks.Models;

// Type aliases (should be at the end)
global using TaskStatus = Assignment.Domain.Tasks.Models.TaskStatus;