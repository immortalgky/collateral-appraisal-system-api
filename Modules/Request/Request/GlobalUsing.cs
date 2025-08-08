// ===== SYSTEM & FRAMEWORK =====
global using System;
global using System.Linq.Expressions;
global using System.Reflection;
global using System.Text.Json.Serialization;

// ===== MICROSOFT ASP.NET CORE =====
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Logging;

// ===== ENTITY FRAMEWORK =====
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ===== THIRD-PARTY LIBRARIES =====
global using Carter;
global using FluentValidation;
global using MediatR;
global using Mapster;

// ===== SHARED INFRASTRUCTURE =====
global using Shared.DDD;
global using Shared.Data;
global using Shared.Data.Extensions;
global using Shared.Data.Seed;
global using Shared.Exceptions;
global using Shared.Contracts.CQRS;

// ===== REQUEST MODULE - CORE =====
global using Request.Data;
global using Request.Data.Repository;
global using Request.Data.Seed;

// ===== REQUEST MODULE - CONTRACTS =====
global using Request.Contracts.Requests.Dtos;
global using Request.Contracts.Requests.Features.GetRequestById;

// ===== REQUEST MODULE - AGGREGATES =====
// Requests (Main Aggregate)
global using Request.Requests;
global using Request.Requests.Models;
global using Request.Requests.Events;
global using Request.Requests.ValueObjects;
global using Request.Requests.Exceptions;
global using Request.Requests.Specifications;

// RequestTitles (Sub-Aggregate)
global using Request.RequestTitles;
global using Request.RequestTitles.Models;
global using Request.RequestTitles.ValueObjects;
global using Request.RequestTitles.Exceptions;
global using Request.RequestTitles.Events;
global using Request.RequestTitles.EventHandlers;
global using Request.RequestTitles.Specifications;
global using Request.RequestTitles.Features.AddRequestTitle;
global using Request.RequestTitles.Features.RemoveRequestTitle;

// RequestComments (Sub-Aggregate)
global using Request.RequestComments;
global using Request.RequestComments.Models;
global using Request.RequestComments.Exceptions;
global using Request.RequestComments.Events;
global using Request.RequestComments.EventHandlers;
global using Request.RequestComments.Specifications;