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
global using MassTransit;

// ===== SHARED INFRASTRUCTURE =====
global using Shared.DDD;
global using Shared.CQRS;
global using Shared.Data;
global using Shared.Data.Extensions;
global using Shared.Data.Seed;
global using Shared.Exceptions;
global using Shared.Models;
global using Shared.Time;
global using Shared.Messaging.Events;

// ===== REQUEST MODULE - INFRASTRUCTURE =====
global using Request.Infrastructure;
global using Request.Infrastructure.Configurations;
global using Request.Infrastructure.Repositories;
global using Request.Infrastructure.Seed;

// ===== REQUEST MODULE - CONTRACTS =====
global using Request.Contracts.Requests.Dtos;
global using Request.Contracts.RequestDocuments.Dto;

// ===== REQUEST MODULE - DOMAIN (Organized by Aggregate) =====
// Requests Aggregate (includes RequestDocument as child entity)
global using Request.Domain.Requests;
global using Request.Domain.Requests.Events;
global using Request.Domain.Requests.Exceptions;

// RequestTitles Aggregate (includes TitleDocument as child entity)
global using Request.Domain.RequestTitles;
global using Request.Domain.RequestTitles.TitleTypes;
global using Request.Domain.RequestTitles.Events;
global using Request.Domain.RequestTitles.Exceptions;

// RequestComments Aggregate
global using Request.Domain.RequestComments;
global using Request.Domain.RequestComments.Events;
global using Request.Domain.RequestComments.Exceptions;

// ===== REQUEST MODULE - APPLICATION LAYER =====
// Event Handlers
global using Request.Application.EventHandlers.Request;
global using Request.Application.EventHandlers.RequestComment;

// Services
global using Request.Application.Services;

// Configurations
global using Request.Application.Configurations;

// ===== REQUEST MODULE - EXTENSIONS =====
global using Request.Extensions;