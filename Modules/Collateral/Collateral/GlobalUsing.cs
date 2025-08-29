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
global using Shared.Dtos;
global using Shared.Data.Extensions;
global using Shared.Contracts.CQRS;
global using Shared.Exceptions;

global using System.Reflection;

global using Collateral.Data;
global using Collateral.Collateral.Shared.Models;
global using Collateral.Collateral.Shared.ValueObjects;
global using Collateral.CollateralMasters.ValueObjects;
global using Collateral.CollateralProperties.Models;
global using Collateral.CollateralProperties.ValueObjects;
global using Collateral.CollateralMachines.Models;
global using Collateral.CollateralMasters.Models;
global using Collateral.CollateralVehicles.Models;
global using Collateral.CollateralVessels.Models;
global using Collateral.Extensions;

global using Collateral.Data.Repository;
global using Collateral.Services;
