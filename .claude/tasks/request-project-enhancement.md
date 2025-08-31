# Request Project Enhancement Plan

## Overview
Comprehensive enhancement plan for the Request project covering OpenAPI specifications, using statement refactoring, and Domain-Driven Design improvements.

## 1. **OpenAPI Specifications for All Aggregates**

### **Current State Analysis:**
- ✅ **Request aggregate**: All 5 endpoints have OpenAPI specs
- ✅ **RequestTitle aggregate**: All 5 endpoints have OpenAPI specs  
- ❌ **RequestComment aggregate**: 3 endpoints missing OpenAPI specs

### **Missing OpenAPI Endpoints:**
- `AddRequestCommentEndpoint` - POST /requests/{requestId}/comments
- `RemoveRequestCommentEndpoint` - DELETE /requests/{requestId}/comments/{commentId}
- `UpdateRequestCommentEndpoint` - PUT /requests/{requestId}/comments/{commentId}

### **Missing CRUD Operations:**
- **RequestComment** needs GET endpoints:
  - GET /requests/{requestId}/comments (get all comments for request)
  - GET /requests/{requestId}/comments/{commentId} (get comment by ID)

## 2. **GlobalUsing.cs Refactoring & Categorization**

### **Current Issues:**
- Poor organization and categorization
- Missing RequestComments namespaces
- Inconsistent namespace grouping

### **Proposed Structure:**
```csharp
// ===== SYSTEM & FRAMEWORK =====
global using System;
global using System.Reflection;
global using System.Text.Json.Serialization;

// ===== MICROSOFT ASP.NET CORE =====
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;

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

// RequestTitles (Sub-Aggregate)
global using Request.RequestTitles;
global using Request.RequestTitles.Models;
global using Request.RequestTitles.ValueObjects;
global using Request.RequestTitles.Exceptions;

// RequestComments (Sub-Aggregate)
global using Request.RequestComments;
global using Request.RequestComments.Models;
global using Request.RequestComments.Exceptions;
```

## 3. **Using Statement Cleanup**

### **Analysis Results:**
- **9 files** use `Request.RequestTitles.Dtos` (needs explicit import due to conflict)
- **8 files** use `Request.RequestComments.Models` (can be global)
- **4 files** use `Request.RequestTitles.Models` (already global)
- **Multiple migration files** have unique imports (keep as-is)

### **Cleanup Strategy:**
- Remove redundant using statements from ~20 files
- Add specific imports only where namespace conflicts exist
- Maintain explicit imports for DTOs to avoid ambiguity

## 4. **DDD Improvements Needed**

### **🔴 Critical Issues:**

#### **A. Missing Domain Services**
- No `RequestDomainService` for complex business logic
- Missing validation for business rules across aggregates

#### **B. Anemic Domain Models**
- Value Objects lack business methods
- Missing domain invariants enforcement
- Limited encapsulation in aggregates

#### **C. Repository Pattern Issues**
- Query methods mixed with command repositories
- Missing specification pattern for complex queries
- No separation between read/write repositories

#### **D. Missing Domain Events**
- `RequestTitleCreated`, `RequestCommentAdded` events not implemented
- No event-driven communication between aggregates

### **🟡 Moderate Issues:**

#### **E. Value Object Improvements**
- `RequestStatus` should have business methods (CanTransitionTo, etc.)
- `Address` validation logic missing
- Missing factory methods for complex value objects

#### **F. Aggregate Boundaries**
- `RequestComment` might be better as part of `Request` aggregate
- `RequestTitle` relationship needs clearer boundaries

#### **G. Missing Specifications**
- No specification pattern for complex business queries
- Hard-coded query logic in handlers

### **🟢 Minor Improvements:**

#### **H. Consistency Issues**
- Inconsistent naming conventions (some handlers end with "Handler", others don't)
- Missing XML documentation on public APIs
- Inconsistent exception handling patterns

## 5. **Implementation Tasks**

### **Phase 1: OpenAPI & Using Cleanup (High Priority)**
1. Add OpenAPI specs to 3 RequestComment endpoints
2. Create missing GET endpoints for RequestComments
3. Refactor GlobalUsing.cs with proper categorization
4. Clean up redundant using statements in ~20 files

### **Phase 2: DDD Core Improvements (Medium Priority)**
1. Implement missing domain events
2. Add domain services for complex business logic
3. Separate read/write repositories using CQRS pattern
4. Add specification pattern for complex queries

### **Phase 3: Advanced DDD (Low Priority)**
1. Enrich value objects with business methods
2. Add domain invariants and validation
3. Review aggregate boundaries and relationships
4. Implement domain event-driven workflows

## **File Impact Summary:**
- **~30 endpoint files** - Add OpenAPI specifications
- **~20 feature files** - Clean using statements  
- **1 GlobalUsing.cs** - Complete refactor
- **3 missing query operations** - New CRUD endpoints
- **Repository interfaces** - Add specifications pattern
- **Domain models** - Add business methods and events

## **Success Criteria:**
- All endpoints have comprehensive OpenAPI documentation
- Clean, organized using statements with proper global imports
- Rich domain models with business logic encapsulation
- Clear separation of concerns following DDD principles
- Event-driven communication between aggregates
- Specification pattern for complex queries

This plan ensures comprehensive coverage of all aggregates while maintaining clean architecture and following DDD best practices.