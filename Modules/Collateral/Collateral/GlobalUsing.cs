global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using Microsoft.Extensions.Configuration;
global using Microsoft.AspNetCore.Routing;

global using Carter;
global using MediatR;
global using Mapster;

global using Shared.DDD;
global using Shared.Data;
global using Shared.Data.Extensions;
global using Shared.Data.Outbox;
global using Shared.CQRS;
global using Shared.Exceptions;
global using Shared.Pagination;

global using System.Reflection;

global using Collateral.Data;
global using Collateral.Data.Repository;
global using Collateral.CollateralMasters.Models;
global using Collateral.CollateralMasters.Services;
global using Collateral.Application.Features.CollateralMasters.Shared;

global using Microsoft.Extensions.Logging;
