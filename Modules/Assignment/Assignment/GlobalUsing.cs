<<<<<<< HEAD
﻿global using Shared.DDD;
global using Assignment.Assignments.Models;
global using Assignment.Assignments.Events;
global using System.Reflection;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using Assignment.Data;
global using Assignment.Data.Seed;
global using Shared.Data;
global using Shared.Data.Seed;
global using Assignment.Assignments.Dtos;
global using Shared.Contracts.CQRS;
global using MediatR;
global using Mapster;
global using Carter;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using FluentValidation;
global using Assignment.Assignments.Exceptions;
global using Assignment.Assignments.ValueObjects;
global using System.Text.Json.Serialization;
global using Shared.Data.Extensions;
global using Assignment.Data.Repository;
global using Shared.Exceptions;
=======
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
global using Shared.Contracts.CQRS;
global using Shared.Data.Extensions;
global using Shared.DDD;
global using Shared.Exceptions;
global using Shared.Time;

// Assignment module namespaces (organized by feature)
global using Assignment.AssigneeSelection.Core;
global using Assignment.AssigneeSelection.Factories;
global using Assignment.AssigneeSelection.Strategies;
global using Assignment.Data;
global using Assignment.Data.Repository;
global using Assignment.Events;
global using Assignment.Sagas.AppraisalSaga;
global using Assignment.Sagas.Models;
global using Assignment.Services;
global using Assignment.Services.Groups;
global using Assignment.Services.Hashing;
global using Assignment.Tasks.Models;

// Type aliases (should be at the end)
global using TaskStatus = Assignment.Tasks.ValueObjects.TaskStatus;
>>>>>>> 3434ca92f42e614d268511d3a6e0d95cb6f4d666
