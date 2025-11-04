---
name: Documentation Task
about: For API documentation, technical docs, and README updates
title: '[DOC] '
labels: documentation
assignees: ''
---

## 📚 Documentation Overview

**Task ID**: <!-- e.g., DOC-API-001 -->
**Documentation Type**: <!-- API / Technical / User Guide / README -->
**Sprint**: <!-- Sprint 1, 2, 3, or 4 -->
**Estimated Time**: <!-- e.g., 3 hours -->
**Priority**: <!-- Critical, High, Medium, Low -->

## 📝 Description

<!-- Brief description of what needs to be documented -->

## ✅ Task Checklist

<!-- List all documentation tasks with time estimates -->
- [ ] **Write content** - Create documentation content (Xh)
- [ ] **Add examples** - Include code examples and samples (Xh)
- [ ] **Add diagrams** - Create visual aids (Xh)
- [ ] **Review accuracy** - Verify technical correctness (Xh)
- [ ] **Update navigation** - Update table of contents/links (Xh)

**Total Estimated Time**: X hours

## 🎯 Acceptance Criteria

<!-- Clear definition of done -->
- [ ] Documentation complete and accurate
- [ ] All code examples tested and working
- [ ] Diagrams/images included where helpful
- [ ] Grammar and spelling checked
- [ ] Links verified
- [ ] Reviewed by technical lead
- [ ] Published/deployed

## 🔗 Dependencies

<!-- Issues that must be completed before this one -->
- Depends on: #issue-number (implementation must be complete)
- Blocks: #issue-number

## 💡 Documentation Structure

### API Documentation

**For OpenAPI/Swagger:**
```csharp
/// <summary>
/// Creates a new appraisal request
/// </summary>
/// <param name="request">The request details</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The ID of the created request</returns>
/// <response code="201">Request created successfully</response>
/// <response code="400">Invalid request data</response>
/// <response code="401">Unauthorized</response>
/// <response code="500">Internal server error</response>
[HttpPost]
[ProducesResponseType(typeof(CreateRequestResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateRequest(
    [FromBody] CreateRequestRequest request,
    CancellationToken cancellationToken)
{
    // Implementation
}
```

**Example OpenAPI Description:**
```yaml
/api/requests:
  post:
    summary: Create a new appraisal request
    description: |
      Creates a new appraisal request in the system. The request will be in Draft status
      and must be submitted before an appraisal can be created.

      **Business Rules:**
      - Loan amount must be positive
      - Property type must be valid
      - User must have 'request.create' permission

    tags:
      - Requests
    requestBody:
      required: true
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/CreateRequestRequest'
          example:
            loanAmount: 1000000
            propertyType: "LandAndBuilding"
            purpose: "Purchase"
    responses:
      '201':
        description: Request created successfully
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreateRequestResponse'
            example:
              id: "3fa85f64-5717-4562-b3fc-2c963f66afa6"
              requestNumber: "REQ-2025-00001"
      '400':
        description: Invalid request data
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/ProblemDetails'
```

### Technical Documentation

**Module Documentation Template:**
```markdown
# [Module Name] Module

## Overview
Brief description of what this module does and its purpose in the system.

## Architecture

### Components
- **Aggregates**: List of main aggregates
- **Value Objects**: Key value objects
- **Domain Events**: Events published by this module
- **Commands**: Write operations
- **Queries**: Read operations

### Dependencies
- Module dependencies
- External services
- Infrastructure requirements

## Database Schema

### Tables
1. **TableName1**
   - Purpose: ...
   - Key fields: ...
   - Relationships: ...

2. **TableName2**
   - Purpose: ...

### Indexes
- Important indexes for performance

## API Endpoints

### Create [Entity]
- **Endpoint**: `POST /api/[entities]`
- **Auth**: Requires authentication
- **Permissions**: `[entity].create`
- **Request**:
  ```json
  {
    "field1": "value",
    "field2": "value"
  }
  ```
- **Response** (201):
  ```json
  {
    "id": "guid",
    "entityNumber": "ENT-2025-00001"
  }
  ```

## Business Rules
1. Rule 1 description
2. Rule 2 description
3. Rule 3 description

## Events

### [EntityCreated]Event
**Published When**: Entity is created
**Payload**:
```csharp
public record EntityCreatedEvent(
    Guid EntityId,
    string EntityNumber,
    DateTime CreatedAt
);
```
**Consumers**: List modules that consume this event

## Error Handling

### Common Errors
| Error Code | Description | Resolution |
|------------|-------------|------------|
| ENT_001 | Entity not found | Verify entity ID |
| ENT_002 | Invalid status transition | Check allowed transitions |

## Examples

### Complete Workflow Example
```csharp
// Step 1: Create entity
var createResponse = await client.PostAsync("/api/entities", createRequest);
var entity = await createResponse.Content.ReadFromJsonAsync<EntityResponse>();

// Step 2: Update entity
var updateRequest = new UpdateEntityRequest { ... };
await client.PutAsync($"/api/entities/{entity.Id}", updateRequest);

// Step 3: Submit entity
await client.PostAsync($"/api/entities/{entity.Id}/submit", null);
```

## Testing
- Unit test coverage: X%
- Integration test scenarios
- Performance benchmarks

## Deployment Notes
- Migration requirements
- Configuration settings
- Environment variables

## Troubleshooting

### Common Issues
1. **Issue**: Description
   **Solution**: Resolution steps

2. **Issue**: Description
   **Solution**: Resolution steps
```

### README Updates

**Sections to include:**
```markdown
# Module Name

Brief description

## Features
- Feature 1
- Feature 2
- Feature 3

## Installation

```bash
# Prerequisites
dotnet --version  # Should be 9.0+

# Install dependencies
dotnet restore

# Run migrations
dotnet ef database update --project Modules/ModuleName
```

## Configuration

```json
{
  "ModuleName": {
    "Setting1": "value",
    "Setting2": "value"
  }
}
```

## Usage

### Basic Example
```csharp
// Code example
```

### Advanced Example
```csharp
// More complex example
```

## API Documentation
See `/docs/api/module-name.md` for detailed API documentation

## Contributing
Guidelines for contributing to this module

## License
MIT License
```

## 📊 Documentation Checklist

### Content Quality
- [ ] Clear and concise writing
- [ ] Accurate technical information
- [ ] No jargon without explanation
- [ ] Consistent terminology
- [ ] Proper grammar and spelling

### Completeness
- [ ] All features documented
- [ ] All API endpoints documented
- [ ] Configuration options explained
- [ ] Examples provided
- [ ] Troubleshooting section included

### Technical Accuracy
- [ ] Code examples tested
- [ ] URLs and links verified
- [ ] Version numbers correct
- [ ] Screenshots up-to-date (if applicable)

### Accessibility
- [ ] Clear headings and structure
- [ ] Table of contents (for long docs)
- [ ] Alt text for images
- [ ] Code blocks properly formatted
- [ ] Links descriptive

## 📚 Documentation Tools

### Markdown
- Use GitHub-flavored markdown
- Use fenced code blocks with language specification
- Use tables for structured data
- Use headings for hierarchy

### Diagrams
- Use Mermaid for flowcharts and diagrams
- Use PlantUML for UML diagrams
- Use draw.io for complex diagrams
- Export as SVG for scalability

### API Documentation
- OpenAPI/Swagger for REST APIs
- XML comments in code
- Postman collections for examples

## 🔍 Review Checklist

Before marking as complete:
- [ ] Read through entire document
- [ ] Test all code examples
- [ ] Verify all links
- [ ] Check formatting
- [ ] Get peer review
- [ ] Spell check
- [ ] Grammar check

## 💡 Best Practices

1. **Write for your audience**
   - Technical docs: developers
   - User guides: end users
   - API docs: API consumers

2. **Keep it up-to-date**
   - Update docs with code changes
   - Version documentation
   - Mark deprecated features

3. **Use examples**
   - Show, don't just tell
   - Provide working code
   - Include common use cases

4. **Be consistent**
   - Follow style guide
   - Use consistent terminology
   - Maintain consistent structure

## 📌 Notes

<!-- Any additional context, warnings, or considerations -->
- **Maintenance**: Documentation should be updated with every feature change
- **Location**: All docs in `/docs` directory
- **Format**: Use Markdown for all documentation
- **Review**: All documentation must be reviewed before merging

---

**Epic**: #epic-issue-number
**Milestone**: Sprint X - [Phase Name]
**Assignee Suggestion**: Technical Writer / Developer X
**Related Implementation**: #issue-number
