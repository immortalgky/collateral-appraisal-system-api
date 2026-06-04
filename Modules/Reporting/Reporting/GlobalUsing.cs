// System
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Threading;
global using System.Threading.Tasks;

// ASP.NET Core
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;

// Microsoft Extensions
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Hosting;

// Third-party
global using MediatR;
global using Carter;
global using Dapper;

// Shared
global using Shared.Data;
global using Shared.CQRS;
global using Shared.Exceptions;
