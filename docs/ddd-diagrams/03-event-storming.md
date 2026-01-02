# Event Storming - Collateral Appraisal System

## Overview

This document represents the outcomes of an Event Storming workshop for the Collateral Appraisal System. Event Storming is a collaborative technique to explore complex business domains through domain events, commands, aggregates, and actors.

## Event Storming Legend

| Color | Element | Description |
|-------|---------|-------------|
| 🟧 **Orange** | **Domain Event** | Something that happened in the past (verb in past tense) |
| 🟦 **Blue** | **Command** | Action that causes an event (verb in imperative) |
| 🟨 **Yellow** | **Aggregate** | Cluster of domain objects that enforce business rules |
| 🟩 **Green** | **Actor** | Person or system that triggers commands |
| 🟥 **Red** | **Hotspot** | Issue, question, or point of conflict |
| 🟪 **Purple** | **Policy** | Automated reaction to events ("Whenever X happens, then Y") |
| 📖 **White** | **Read Model** | Information needed to make decisions |
| ⚡ **Lightning** | **External System** | Integration with external services |

## Complete Event Flow Timeline

```mermaid
graph LR
    %% Request Phase
    subgraph "Request Creation & Submission"
        A1[🟩 RM] -->|🟦 Create Request| A2[RequestCreated]
        A2 --> A3[🟨 Request]
        A1 -->|🟦 Add Customer| A4[CustomerAdded]
        A4 --> A3
        A1 -->|🟦 Add Title Deed| A5[TitleDeedInfoAdded]
        A5 --> A3
        A1 -->|🟦 Attach Document| A6[DocumentAttached]
        A6 --> A3
        A1 -->|🟦 Submit Request| A7[RequestSubmitted]
        A7 --> A3
    end

    %% Appraisal Creation
    subgraph "Appraisal Assignment"
        A7 -.->|🟪 Policy: Auto-create| B1[AppraisalCreated]
        B1 --> B2[🟨 Appraisal]
        B3[🟩 Admin] -->|🟦 Review Request| B3A[RequestReviewed]
        B3A --> B2
        B3 -->|🟦 Assign Appraiser| B4[AppraisalAssigned]
        B4 --> B2
        B5[🟩 Appraiser] -->|🟦 Accept Assignment| B6[AssignmentAccepted]
        B6 --> B2
    end

    %% Field Survey
    subgraph "Field Survey & Photo Collection"
        B5 -->|🟦 Start Survey| C1[FieldSurveyStarted]
        C1 --> C2[🟨 FieldSurvey]
        B5 -->|🟦 Upload Photo| C3[PhotoUploaded]
        C3 --> C4[🟨 GalleryPhoto]
        B5 -->|🟦 Record GPS| C5[LocationCaptured]
        C5 --> C4
        B5 -->|🟦 Complete Survey| C6[FieldSurveyCompleted]
        C6 --> C2
    end

    %% Property Analysis
    subgraph "Property Analysis & Valuation"
        B5 -->|🟦 Create Land Details| D1[LandDetailsCreated]
        D1 --> D2[🟨 LandAppraisalDetail]
        B5 -->|🟦 Link Photo to Section| D3[PhotoLinkedToProperty]
        D3 --> D2
        B5 -->|🟦 Add Comparable| D4[ComparablePropertyAdded]
        D4 --> D5[🟨 ValuationAnalysis]
        B5 -->|🟦 Calculate Valuation| D6[ValuationCalculated]
        D6 --> D5
        B5 -->|🟦 Submit for Review| D7[AppraisalSubmittedForReview]
        D7 --> B2
    end

    %% Review Workflow
    subgraph "Review & Approval"
        E1[🟩 Checker] -->|🟦 Review Appraisal| E2[AppraisalChecked]
        E2 --> E3[🟨 AppraisalReview]
        E4[🟩 Verifier] -->|🟦 Verify Appraisal| E5[AppraisalVerified]
        E5 --> E3
        E6[🟩 Committee] -->|🟦 Approve Appraisal| E7[AppraisalApproved]
        E7 --> E3
        E7 --> E8[AppraisalCompleted]
        E8 --> B2
    end

    %% Collateral Creation
    subgraph "Collateral Management"
        E8 -.->|🟪 Policy: Auto-create| F1[CollateralCreated]
        F1 --> F2[🟨 Collateral]
        F3[🟩 System] -->|🟦 Schedule Revaluation| F4[RevaluationScheduled]
        F4 --> F2
        F5[🟩 Risk Manager] -->|🟦 Update Risk Rating| F6[RiskRatingChanged]
        F6 --> F2
    end

    %% Document Finalization
    subgraph "Document Management"
        G1[🟩 Appraiser] -->|🟦 Upload Document| G2[DocumentUploaded]
        G2 --> G3[🟨 Document]
        G4[🟩 System] -->|🟦 Create Version| G5[DocumentVersionCreated]
        G5 --> G3
        G6[🟩 Admin] -->|🟦 Grant Access| G7[AccessGranted]
        G7 --> G8[🟨 DocumentAccess]
        G9[🟩 User] -->|🟦 View Document| G10[DocumentAccessed]
        G10 --> G3
    end

    classDef event fill:#FFA500,stroke:#333,stroke-width:2px,color:#000
    classDef aggregate fill:#FFFF00,stroke:#333,stroke-width:2px,color:#000
    classDef actor fill:#90EE90,stroke:#333,stroke-width:2px,color:#000
    classDef policy fill:#DDA0DD,stroke:#333,stroke-width:2px,color:#000
```

## Detailed Event Storming Board

### Phase 1: Request Creation (Request Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟩 RM | 🟦 Create Request | 🟧 RequestCreated | 🟨 Request | | |
| 🟩 RM | 🟦 Add Customer | 🟧 CustomerAdded | 🟨 Request | | 🟥 How to handle multiple borrowers? |
| 🟩 RM | 🟦 Add Property Type | 🟧 PropertyTypeAdded | 🟨 Request | | 🟥 Single or multiple properties? |
| 🟩 RM | 🟦 Enter Title Deed | 🟧 TitleDeedInfoAdded | 🟨 Request | | 🟥 Validation rules for deed types? |
| 🟩 RM | 🟦 Attach Document | 🟧 DocumentAttached | 🟨 Request | | |
| 🟩 RM | 🟦 Set Priority | 🟧 PrioritySet | 🟨 Request | | 🟥 SLA based on priority? |
| 🟩 RM | 🟦 Submit Request | 🟧 RequestSubmitted | 🟨 Request | 🟪 Trigger AppraisalCreated | |
| ⚡ LOS | 🟦 Import Request | 🟧 RequestImported | 🟨 Request | | 🟥 Data mapping from LOS? |

**Read Models Needed:**
- 📖 Available Appraisers List
- 📖 Title Deed Validation Rules
- 📖 Document Templates

**Business Rules:**
- Request must have at least one customer
- Request must have at least one property type
- Title deed info required before submission
- Loan amount must be positive

---

### Phase 2: Appraisal Assignment (Appraisal Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟪 System | 🟦 Create Appraisal | 🟧 AppraisalCreated | 🟨 Appraisal | 🟪 When RequestSubmitted | |
| 🟩 Admin | 🟦 Review Request Details | 🟧 RequestReviewed | 🟨 Appraisal | 🟪 Notify Admin for assignment | |
| 🟩 Admin | 🟦 View Assignment Recommendations | 🟧 RecommendationsViewed | 🟨 Appraisal | 🟪 System provides top 5 appraisers | 🟥 Recommendation algorithm accuracy? |
| 🟩 Admin | 🟦 Assign Appraiser | 🟧 AppraisalAssigned | 🟨 Appraisal | 🟪 Manual assignment based on criteria | 🟥 Can admin override recommendations? |
| 🟩 Appraiser | 🟦 Accept Assignment | 🟧 AssignmentAccepted | 🟨 Appraisal | 🟪 Notify RM and Admin | |
| 🟩 Appraiser | 🟦 Reject Assignment | 🟧 AssignmentRejected | 🟨 Appraisal | 🟪 Notify Admin for reassignment | 🟥 Max rejections allowed? |
| 🟩 Admin | 🟦 Reassign | 🟧 AppraisalReassigned | 🟨 Appraisal | 🟪 After rejection | |

**Read Models Needed:**
- 📖 Pending Assignments Dashboard (Admin view)
- 📖 Appraiser Recommendations with Scores
- 📖 Appraiser Workload Dashboard
- 📖 Appraiser Locations & Specializations
- 📖 Request Details with Property Information

**Assignment Criteria:**
- 📊 Location Proximity (40%): Distance from property location
- 📊 Current Workload (30%): Active assignments count
- 📊 Performance Score (20%): Historical quality and timeliness
- 📊 Specialization (10%): Property type expertise

**Business Rules:**
- Admin must review and manually assign all appraisals
- System provides top 5 recommended appraisers with scores
- Admin can override recommendations with justification
- Appraiser must have correct certifications for property type
- Cannot assign to same appraiser twice in a row for same customer
- Due date must be within SLA (5-10 business days)
- Assignment status: PendingAssignment → Assigned → Accepted/Rejected

---

### Phase 3: Field Survey (Appraisal Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟩 Appraiser | 🟦 Schedule Survey | 🟧 SurveyScheduled | 🟨 FieldSurvey | | |
| 🟩 Appraiser | 🟦 Start Survey | 🟧 SurveyStarted | 🟨 FieldSurvey | 🟪 Enable GPS tracking | |
| 🟩 Appraiser | 🟦 Upload Photo | 🟧 PhotoUploaded | 🟨 GalleryPhoto | 🟪 Store in Document module | |
| 🟩 Appraiser | 🟦 Record GPS | 🟧 LocationCaptured | 🟨 GalleryPhoto | 🟪 Auto-capture | |
| 🟩 Appraiser | 🟦 Categorize Photo | 🟧 PhotoCategorized | 🟨 GalleryPhoto | | 🟥 Categories consistent? |
| 🟩 Appraiser | 🟦 Record Video | 🟧 VideoRecorded | 🟨 VideoRecording | | 🟥 Video size limits? |
| 🟩 Appraiser | 🟦 Add Voice Note | 🟧 VoiceNoteAdded | 🟨 AudioNote | | |
| 🟩 Appraiser | 🟦 Complete Survey | 🟧 SurveyCompleted | 🟨 FieldSurvey | 🟪 Notify for office work | |

**Read Models Needed:**
- 📖 Photo Gallery with Thumbnails
- 📖 Survey Checklist
- 📖 GPS Location Map

**Business Rules:**
- Survey must be completed within 3 days of acceptance
- Minimum 10 photos required
- GPS coordinates required for all photos
- Photos must be taken within 100m of property location

---

### Phase 4: Property Analysis (Appraisal Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟩 Appraiser | 🟦 Create Land Details | 🟧 LandDetailsCreated | 🟨 LandAppraisalDetail | | 🟥 Which property type? |
| 🟩 Appraiser | 🟦 Create Building Details | 🟧 BuildingDetailsCreated | 🟨 BuildingAppraisalDetail | | |
| 🟩 Appraiser | 🟦 Create Condo Details | 🟧 CondoDetailsCreated | 🟨 CondoAppraisalDetail | | |
| 🟩 Appraiser | 🟦 Link Photo to Section | 🟧 PhotoLinkedToSection | 🟨 PropertyPhotoMapping | | 🟥 Photo can be reused? |
| 🟩 Appraiser | 🟦 Annotate Photo | 🟧 PhotoAnnotated | 🟨 PhotoAnnotation | | |
| 🟩 Appraiser | 🟦 Mark for Report | 🟧 PhotoMarkedForReport | 🟨 GalleryPhoto | | |

**Read Models Needed:**
- 📖 Photo Gallery (filtered by category)
- 📖 Property Detail Form (type-specific)
- 📖 Photo Mapping View

**Business Rules:**
- One property detail per property type
- Photos can be linked to multiple sections
- All critical sections must have at least one photo

---

### Phase 5: Valuation Analysis (Appraisal Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟩 Appraiser | 🟦 Add Comparable | 🟧 ComparableAdded | 🟨 ValuationAnalysis | | 🟥 Min comparables? |
| 🟩 Appraiser | 🟦 Apply Adjustments | 🟧 AdjustmentsApplied | 🟨 ValuationAnalysis | | |
| 🟩 Appraiser | 🟦 Calculate Market Value | 🟧 MarketValueCalculated | 🟨 ValuationAnalysis | 🟪 Auto-calculate | |
| 🟩 Appraiser | 🟦 Set Appraised Value | 🟧 AppraisedValueSet | 🟨 ValuationAnalysis | | 🟥 Must be <= Market? |
| 🟩 Appraiser | 🟦 Calculate Forced Sale | 🟧 ForcedSaleValueCalculated | 🟨 ValuationAnalysis | 🟪 80% of appraised | |
| 🟩 Appraiser | 🟦 Generate Report | 🟧 ReportGenerated | 🟨 AppraisalReport | | |
| 🟩 Appraiser | 🟦 Submit for Review | 🟧 SubmittedForReview | 🟨 Appraisal | 🟪 Notify Checker | |

**Read Models Needed:**
- 📖 Comparable Properties Database
- 📖 Market Price Trends
- 📖 Valuation Calculator

**Business Rules:**
- Minimum 3 comparable properties required
- Market value must be within 20% of comparables average
- Appraised value ≤ Market value
- Forced sale value = 80% of appraised value (default)

---

### Phase 6: Review & Approval (Appraisal Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟩 Checker | 🟦 Review Appraisal | 🟧 AppraisalReviewed | 🟨 AppraisalReview | | |
| 🟩 Checker | 🟦 Approve (Checker) | 🟧 CheckerApproved | 🟨 AppraisalReview | 🟪 Notify Verifier | |
| 🟩 Checker | 🟦 Reject (Checker) | 🟧 CheckerRejected | 🟨 AppraisalReview | 🟪 Return to Appraiser | 🟥 Max rejections? |
| 🟩 Verifier | 🟦 Verify Appraisal | 🟧 AppraisalVerified | 🟨 AppraisalReview | 🟪 Notify Committee | |
| 🟩 Verifier | 🟦 Reject (Verifier) | 🟧 VerifierRejected | 🟨 AppraisalReview | 🟪 Return to Appraiser | |
| 🟩 Committee | 🟦 Approve (Committee) | 🟧 CommitteeApproved | 🟨 AppraisalReview | | |
| 🟩 Committee | 🟦 Final Approval | 🟧 AppraisalCompleted | 🟨 Appraisal | 🟪 Trigger CollateralCreated | |
| 🟩 Committee | 🟦 Reject (Committee) | 🟧 CommitteeRejected | 🟨 AppraisalReview | 🟪 Return to Appraiser | |

**Read Models Needed:**
- 📖 Appraisal Review Checklist
- 📖 Approval History
- 📖 Committee Member Assignments

**Business Rules:**
- Sequential review: Checker → Verifier → Committee
- Each level can approve or reject (not both)
- Rejection requires detailed reason
- Maximum 2 rejections, then escalate to supervisor

---

### Phase 7: Collateral Creation (Collateral Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟪 System | 🟦 Create Collateral | 🟧 CollateralCreated | 🟨 Collateral | 🟪 When AppraisalCompleted | |
| 🟪 System | 🟦 Copy Valuation | 🟧 ValuationCopied | 🟨 Collateral | 🟪 From appraisal | |
| 🟪 System | 🟦 Schedule Revaluation | 🟧 RevaluationScheduled | 🟨 Collateral | 🟪 Based on policy (12 months) | |
| 🟩 Risk Manager | 🟦 Assess Risk | 🟧 RiskAssessed | 🟨 Collateral | | |
| 🟩 Risk Manager | 🟦 Update Risk Rating | 🟧 RiskRatingChanged | 🟨 Collateral | 🟪 Alert if High risk | 🟥 Risk calculation? |
| 🟩 Admin | 🟦 Dispose Collateral | 🟧 CollateralDisposed | 🟨 Collateral | 🟪 Notify LOS | |

**Read Models Needed:**
- 📖 Collateral Portfolio
- 📖 Valuation History
- 📖 Risk Assessment Dashboard

**Business Rules:**
- Collateral activated immediately after creation
- Revaluation required every 12 months (configurable)
- Risk rating based on value change and market conditions

---

### Phase 8: Document Management (Document Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟩 User | 🟦 Upload Document | 🟧 DocumentUploaded | 🟨 Document | 🟪 Virus scan | |
| 🟪 System | 🟦 Scan for Virus | 🟧 DocumentScanned | 🟨 Document | 🟪 If clean, proceed | 🟥 Quarantine process? |
| 🟪 System | 🟦 Generate Thumbnail | 🟧 ThumbnailGenerated | 🟨 Document | 🟪 For images | |
| 🟩 User | 🟦 Create New Version | 🟧 VersionCreated | 🟨 DocumentVersion | | |
| 🟩 Admin | 🟦 Grant Access | 🟧 AccessGranted | 🟨 DocumentAccess | | |
| 🟩 Admin | 🟦 Revoke Access | 🟧 AccessRevoked | 🟨 DocumentAccess | 🟪 Notify user | |
| 🟩 User | 🟦 View Document | 🟧 DocumentAccessed | 🟨 Document | 🟪 Log access | |
| 🟩 User | 🟦 Download Document | 🟧 DocumentDownloaded | 🟨 Document | 🟪 Log access | |

**Read Models Needed:**
- 📖 Document Library
- 📖 Access Permissions Matrix
- 📖 Document Access Logs

**Business Rules:**
- All uploads must pass virus scan
- Maximum file size: 100MB
- Version history maintained for 7 years
- Access logs retained for compliance

---

### Phase 9: Authentication (Auth Context)

| Actor | Command | Event | Aggregate | Policy | Hotspot |
|-------|---------|-------|-----------|--------|---------|
| 🟩 User | 🟦 Login | 🟧 UserLoggedIn | 🟨 User | 🟪 Create session | |
| 🟩 User | 🟦 Logout | 🟧 UserLoggedOut | 🟨 User | 🟪 Clear session | |
| 🟩 Admin | 🟦 Create User | 🟧 UserCreated | 🟨 User | 🟪 Send welcome email | |
| 🟩 Admin | 🟦 Assign Role | 🟧 RoleAssigned | 🟨 UserRole | 🟪 Invalidate cache | |
| 🟩 Admin | 🟦 Grant Permission | 🟧 PermissionGranted | 🟨 UserPermission | 🟪 Invalidate cache | |
| 🟪 System | 🟦 Lock Account | 🟧 AccountLocked | 🟨 User | 🟪 After 5 failed logins | 🟥 Unlock process? |
| 🟩 User | 🟦 Change Password | 🟧 PasswordChanged | 🟨 User | 🟪 Force re-login | |

**Read Models Needed:**
- 📖 User Directory
- 📖 Permission Matrix
- 📖 Login History

**Business Rules:**
- Password must meet complexity requirements
- Account locked after 5 failed login attempts
- Session expires after 30 minutes of inactivity
- Password expires after 90 days

---

## Hotspots & Questions

### Critical Hotspots (🟥)

1. **Multiple Properties per Request**
   - Q: Can one request include multiple property types?
   - A: Yes, using RequestPropertyTypes table (1:many)

2. **Photo Linking Strategy**
   - Q: Can same photo be linked to multiple property sections?
   - A: Yes, via PropertyPhotoMappings (many-to-many)

3. **Assignment Process**
   - Q: Should assignment be automatic or manual?
   - A: Manual by Admin with system-provided recommendations based on location proximity (40%), workload (30%), performance (20%), and specialization (10%)
   - Admin can override recommendations with justification notes

4. **Valuation Bounds**
   - Q: Must appraised value be ≤ market value?
   - A: Yes, business rule enforced in aggregate

5. **Review Rejection Limits**
   - Q: How many times can appraisal be rejected?
   - A: Maximum 2 rejections per level, then escalate

6. **Document Virus Quarantine**
   - Q: What happens to infected files?
   - A: Move to quarantine, alert security team, notify uploader

---

## Policies (Automated Reactions)

| Trigger Event | Policy | Action |
|---------------|--------|--------|
| RequestSubmitted | Auto-create appraisal | Create Appraisal record with status PendingAssignment |
| AppraisalCreated | Notify admin for assignment | Send notification to Admin dashboard |
| AdminViewsRecommendations | Calculate recommendations | Score appraisers by location, workload, performance, specialization |
| AssignmentRejected | Notify admin for reassignment | Send alert to Admin to manually reassign |
| SurveyCompleted | Notify for office work | Send push notification |
| CheckerApproved | Notify verifier | Email + in-app notification |
| VerifierApproved | Notify committee | Email + in-app notification |
| AppraisalCompleted | Create collateral | Create Collateral record |
| CollateralCreated | Schedule revaluation | Create calendar event (12 months) |
| DocumentUploaded | Virus scan | Submit to antivirus service |
| DocumentScanned (clean) | Generate thumbnail | For image files |
| DocumentAccessed | Log access | Create audit log entry |
| LoginFailed (5x) | Lock account | Set LockoutEnd date |
| PasswordChanged | Force re-login | Invalidate all sessions |

---

## Aggregates & Their Boundaries

| Aggregate | Entities Within | Value Objects | Invariants |
|-----------|----------------|---------------|------------|
| **Request** | Request<br/>RequestCustomer<br/>RequestPropertyType<br/>RequestDocument<br/>TitleDeedInfo<br/>RequestStatusHistory | RequestDetail<br/>Contact | • At least 1 customer<br/>• At least 1 property type<br/>• Valid status transitions |
| **Appraisal** | Appraisal<br/>AppraisalAssignment<br/>FieldSurvey<br/>PropertyInformation<br/>ValuationAnalysis<br/>AppraisalReport<br/>AppraisalReview | TimeSlot<br/>Location | • One assignment at a time<br/>• Sequential review process<br/>• Cannot complete without valuation |
| **GalleryPhoto** | GalleryPhoto<br/>PhotoAnnotation | Location<br/>PhotoMetadata | • GPS required<br/>• Valid category |
| **PropertyDetail** | LandAppraisalDetail<br/>BuildingAppraisalDetail<br/>(etc.) | LandArea<br/>Dimensions | • Type-specific validations<br/>• Required fields per type |
| **Collateral** | Collateral<br/>LandCollateral<br/>BuildingCollateral<br/>(etc.)<br/>ValuationHistory | Money | • Active or disposed<br/>• Valuation > 0 |
| **Document** | Document<br/>DocumentVersion<br/>DocumentRelationship<br/>DocumentAccess | FileMetadata | • At least 1 version<br/>• Valid storage URL |
| **User** | User<br/>UserRole<br/>UserPermission<br/>UserOrganization | ContactInfo | • Unique username/email<br/>• Valid password |

---

## External Systems Integration

| System | Integration Type | Events Sent | Events Received |
|--------|-----------------|-------------|-----------------|
| **LOS** | REST API | RequestCreated<br/>CollateralCreated | RequestImported |
| **Email Service** | SMTP/SendGrid | UserCreated<br/>AssignmentNotification | N/A |
| **Cloud Storage** | Azure Blob/AWS S3 | DocumentUploaded | N/A |
| **Antivirus** | REST API | DocumentUploaded | DocumentScanned |
| **Mobile App** | Push Notifications | SurveyScheduled<br/>AssignmentCreated | PhotoUploaded<br/>LocationCaptured |

---

## Next Steps from Event Storming

1. **Validate with Domain Experts**: Review hotspots and business rules
2. **Prioritize Features**: Use event timeline to identify MVP scope
3. **Define Bounded Contexts**: Confirm context boundaries identified
4. **Design Aggregates**: Implement aggregates with invariants
5. **Implement Events**: Code domain and integration events
6. **Build Read Models**: Create query projections for UI

---

**Next**: [04-sequence-diagrams.md](04-sequence-diagrams.md) - Detailed interaction flows for key scenarios
