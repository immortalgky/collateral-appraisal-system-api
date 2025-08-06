global using Microsoft.AspNetCore.Http;

global using NSubstitute;
global using NSubstitute.ExceptionExtensions;

global using Document.Data.Repository;
global using Document.Tests.Common;
global using Document.Services;
global using Document.Documents.Features.GetDocuments;
global using Document.Documents.Features.UpdateDocument;
global using Document.Documents.Features.UploadDocument;
global using Document.Documents.Features.GetDocumentById;
global using Document.Documents.Features.DeleteDocument;

global using Document.Documents.Exceptions;

global using Document.Contracts.Documents.Dtos;

global using FluentValidation.TestHelper;