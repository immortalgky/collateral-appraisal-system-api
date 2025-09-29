// System namespaces
global using System.Collections.Concurrent;
global using System.Reflection;
global using System.Text.Json;

// Microsoft namespaces
global using Microsoft.Data.SqlClient;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using Microsoft.EntityFrameworkCore.Storage;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

// Third-party libraries
global using Dapper;
global using MassTransit;
global using Quartz;

// Project namespaces
global using Shared.Data;
global using Shared.Data.Models;
global using Shared.Exceptions;
global using Shared.Messaging.OutboxPatterns.Configurations;
global using Shared.Messaging.OutboxPatterns.Extensions;
global using Shared.Messaging.OutboxPatterns.Jobs;
global using Shared.Messaging.OutboxPatterns.Repository;
global using Shared.Messaging.OutboxPatterns.Services;
global using Shared.Messaging.OutboxPatterns.Wrappers;