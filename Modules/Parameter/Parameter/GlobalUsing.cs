global using System.Reflection;

global using MediatR;
global using Mapster;
global using Carter;

global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.EntityFrameworkCore.Diagnostics;

global using Parameter.Data;
global using Parameter.Data.Seed;
global using Parameter.Data.Repository;
global using Parameter.Parameters.Extensions;
global using Parameter.Parameters.Exceptions;
global using Parameter.Contracts.Parameters.Dtos;

global using Shared.DDD;
global using Shared.Exceptions;
global using Shared.Extensions;
global using Shared.Data.Extensions;
global using Shared.Data.Seed;
global using Shared.Contracts.CQRS;