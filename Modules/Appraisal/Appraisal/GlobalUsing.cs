// System

global using System;
global using System.Reflection;

// ASP.NET Core
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Routing;

// Entity Framework Core
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Microsoft Extensions
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

// Third-party
global using MediatR;
global using Mapster;
global using Carter;
global using FluentValidation;

// Shared
global using Shared.Data;
global using Shared.Data.Extensions;
global using Shared.DDD;
global using Shared.CQRS;
global using Shared.Exceptions;
global using Shared.Pagination;

// Appraisal - Domain Aggregates
global using Appraisal.Domain.Appraisals;
global using Appraisal.Domain.Appraisals.Events;
global using Appraisal.Domain.Appraisals.Exceptions;
global using Appraisal.Domain.Committees;
global using Appraisal.Domain.DocumentRequirements;
global using Appraisal.Domain.MarketComparables;
global using Appraisal.Domain.Settings;
global using Appraisal.Domain.Quotations;

// Appraisal - Infrastructure
global using Appraisal.Infrastructure;
global using Appraisal.Infrastructure.Repositories;

// Appraisal - Contracts
global using Appraisal.Contracts.Appraisals.Dto;