global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Configuration;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;

global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.EntityFrameworkCore;

global using Appraisal.Data;
global using Appraisal.Appraisal.Shared.ValueObjects;
global using Appraisal.RequestAppraisals.Models;
global using Appraisal.MachineAppraisalDetails.Models;
global using Appraisal.MachineAppraisalDetails.ValueObjects;
global using Appraisal.VehicleAppraisalDetails.Models;
global using Appraisal.VesselAppraisalDetails.Models;
global using Appraisal.AppraisalProperties.Models;

global using Appraisal.Contracts.Appraisals.Dto;

global using Shared.Data.Extensions;
global using Shared.DDD;
global using Shared.Contracts.CQRS;

global using System.Reflection;

global using Appraisal.Service;
global using Mapster;
global using Carter;
global using MediatR;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Shared.Pagination;
global using Microsoft.AspNetCore.Mvc;

global using Appraisal.Data.Repository;
global using Appraisal.Extensions;
global using Appraisal.Exceptions;
global using Appraisal.AppraisalProperties.ValueObjects;
global using Shared.Exceptions;
global using Shared.Data;



