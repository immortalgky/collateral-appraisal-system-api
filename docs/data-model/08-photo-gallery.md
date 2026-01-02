# Photo Gallery & Media Management

## Overview

The photo gallery system supports the **two-phase workflow** where appraisers:
1. **Phase 1 (Site Visit)**: Quickly upload photos from mobile device
2. **Phase 2 (Office)**: Review photos and link them to property detail sections

This approach maximizes efficiency during site visits while enabling comprehensive documentation back at the office.

## Two-Phase Workflow

### Phase 1: Site Visit (Mobile)

**Objective**: Capture as many photos as possible quickly without interrupting the inspection

```
┌─────────────────┐
│  Appraiser      │
│  At Property    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Take Photos    │
│  - Automatic    │
│  - GPS tagged   │
│  - Time stamped │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Quick Upload   │
│  - WiFi/4G      │
│  - Background   │
│  - Auto retry   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ AppraisalGallery│
│  - All photos   │
│  - Organized    │
│  - Accessible   │
└─────────────────┘
```

**Key Features**:
- ✅ One-tap upload
- ✅ Works offline (queue for later)
- ✅ Auto GPS capture
- ✅ Battery optimized
- ✅ Resume interrupted uploads
- ✅ Basic categorization only

### Phase 2: Office Work (Desktop)

**Objective**: Create detailed property information and link relevant photos

```
┌─────────────────┐
│  Back at Office │
│  Review Work    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Open Gallery   │
│  - View all     │
│  - Filter/Sort  │
│  - Preview      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Create Property │
│ Detail Record   │
│ (e.g., Building)│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Select Photos  │
│  - From gallery │
│  - Link to      │
│    sections     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Annotate      │
│  - Highlight    │
│  - Mark defects │
│  - Add notes    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Mark for       │
│  Report         │
└─────────────────┘
```

**Key Features**:
- ✅ Browse all photos from site visit
- ✅ Select multiple photos
- ✅ Link to specific property sections
- ✅ Add detailed annotations
- ✅ Mark photos for report inclusion
- ✅ Create before/after comparisons

## Data Model

### 1. AppraisalGallery

**Purpose**: Container for organizing photos by upload session

```sql
CREATE TABLE appraisal.AppraisalGalleries
(
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AppraisalId     UNIQUEIDENTIFIER NOT NULL,
    GalleryName     NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(500) NULL,
    CreatedOn       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       UNIQUEIDENTIFIER NOT NULL,
    UpdatedOn       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy       UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_AppraisalGallery_Appraisal FOREIGN KEY (AppraisalId)
        REFERENCES appraisal.Appraisals(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AppraisalGallery_CreatedBy FOREIGN KEY (CreatedBy)
        REFERENCES auth.Users(Id),
    CONSTRAINT FK_AppraisalGallery_UpdatedBy FOREIGN KEY (UpdatedBy)
        REFERENCES auth.Users(Id)
);

CREATE INDEX IX_AppraisalGallery_AppraisalId ON appraisal.AppraisalGalleries(AppraisalId);
CREATE INDEX IX_AppraisalGallery_CreatedOn ON appraisal.AppraisalGalleries(CreatedOn DESC);
```

**Usage**:
```csharp
// Automatically create gallery on first photo upload
var gallery = new AppraisalGallery
{
    AppraisalId = appraisalId,
    GalleryName = $"Site Visit - {DateTime.Now:yyyy-MM-dd HH:mm}",
    Description = "Initial site visit photos",
    CreatedBy = currentUserId
};
```

**Organization Examples**:
```
Appraisal #12345
├── Gallery: "Site Visit - 2025-01-15 Morning"
│   ├── 15 photos (Exterior views)
├── Gallery: "Site Visit - 2025-01-15 Afternoon"
│   ├── 23 photos (Interior details)
└── Gallery: "Follow-up Visit - 2025-01-20"
    ├── 8 photos (Repairs verification)
```

### 2. GalleryPhoto

**Purpose**: Individual photo record with metadata and GPS information

```sql
CREATE TABLE appraisal.GalleryPhotos
(
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    GalleryId           UNIQUEIDENTIFIER NOT NULL,
    DocumentId          UNIQUEIDENTIFIER NOT NULL,  -- References document.Documents
    PhotoNumber         INT NOT NULL,               -- Sequential in gallery
    DisplayOrder        INT NOT NULL,

    -- Categorization
    PhotoType           NVARCHAR(50) NOT NULL,      -- Exterior, Interior, Land, Defect, Location, Aerial, Panoramic, Detail
    PhotoCategory       NVARCHAR(50) NOT NULL,      -- Front, Back, Side, Roof, Kitchen, Bathroom, Bedroom, etc.
    Caption             NVARCHAR(200) NULL,
    Description         NVARCHAR(MAX) NULL,

    -- Location (Value Object)
    Latitude            DECIMAL(10, 8) NULL,        -- GPS coordinates
    Longitude           DECIMAL(11, 8) NULL,
    Altitude            DECIMAL(10, 2) NULL,        -- meters
    Accuracy            DECIMAL(10, 2) NULL,        -- meters

    -- Capture Information
    CapturedAt          DATETIME2 NOT NULL,
    CapturedBy          UNIQUEIDENTIFIER NOT NULL,

    -- Camera Information (Value Object)
    DeviceModel         NVARCHAR(100) NULL,
    CameraMake          NVARCHAR(100) NULL,
    CameraModel         NVARCHAR(100) NULL,
    FocalLength         NVARCHAR(50) NULL,
    FlashUsed           BIT NULL,

    -- File Information
    ThumbnailUrl        NVARCHAR(500) NOT NULL,
    FullSizeUrl         NVARCHAR(500) NOT NULL,
    FileSize            BIGINT NOT NULL,            -- bytes
    Width               INT NOT NULL,               -- pixels
    Height              INT NOT NULL,               -- pixels
    AspectRatio         NVARCHAR(10) NULL,          -- e.g., "16:9", "4:3"

    -- Status Flags
    IsSelected          BIT NOT NULL DEFAULT 0,     -- Selected for detailed review
    IsUsedInReport      BIT NOT NULL DEFAULT 0,     -- Include in final report

    -- Tags
    Tags                NVARCHAR(MAX) NULL,         -- JSON array of tags

    -- Upload Information
    UploadedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UploadedBy          UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_GalleryPhoto_Gallery FOREIGN KEY (GalleryId)
        REFERENCES appraisal.AppraisalGalleries(Id) ON DELETE CASCADE,
    CONSTRAINT FK_GalleryPhoto_Document FOREIGN KEY (DocumentId)
        REFERENCES document.Documents(Id),
    CONSTRAINT FK_GalleryPhoto_CapturedBy FOREIGN KEY (CapturedBy)
        REFERENCES auth.Users(Id),
    CONSTRAINT FK_GalleryPhoto_UploadedBy FOREIGN KEY (UploadedBy)
        REFERENCES auth.Users(Id)
);

CREATE INDEX IX_GalleryPhoto_GalleryId ON appraisal.GalleryPhotos(GalleryId);
CREATE INDEX IX_GalleryPhoto_PhotoType ON appraisal.GalleryPhotos(PhotoType);
CREATE INDEX IX_GalleryPhoto_IsUsedInReport ON appraisal.GalleryPhotos(IsUsedInReport) WHERE IsUsedInReport = 1;
CREATE INDEX IX_GalleryPhoto_CapturedAt ON appraisal.GalleryPhotos(CapturedAt DESC);
```

**Photo Metadata Examples**:

```csharp
// Exterior photo with GPS
var photo = new GalleryPhoto
{
    GalleryId = galleryId,
    DocumentId = documentId,
    PhotoNumber = 1,
    DisplayOrder = 1,
    PhotoType = PhotoType.Exterior,
    PhotoCategory = PhotoCategory.Front,
    Caption = "Front elevation view",
    Latitude = 13.7563m,
    Longitude = 100.5018m,  // Bangkok coordinates
    Altitude = 2.5m,
    CapturedAt = DateTime.UtcNow,
    CapturedBy = appraiserId,
    DeviceModel = "iPhone 15 Pro",
    Width = 4032,
    Height = 3024,
    Tags = "[\"entrance\", \"facade\", \"street-view\"]"
};

// Interior defect photo
var defectPhoto = new GalleryPhoto
{
    PhotoType = PhotoType.Defect,
    PhotoCategory = PhotoCategory.Bathroom,
    Caption = "Water damage in bathroom ceiling",
    Description = "Visible water stain, approximately 30cm diameter, likely from roof leak",
    Tags = "[\"water-damage\", \"ceiling\", \"urgent\"]",
    IsSelected = true  // Needs attention
};
```

### 3. PropertyPhotoMapping

**Purpose**: Link gallery photos to specific sections of property detail records

This is the **key table** that connects Phase 1 (photos) to Phase 2 (property details).

```sql
CREATE TABLE appraisal.PropertyPhotoMappings
(
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    PhotoId             UNIQUEIDENTIFIER NOT NULL,
    PropertyDetailType  NVARCHAR(50) NOT NULL,      -- Land, Building, Condo, Vehicle, Vessel, Machinery
    PropertyDetailId    UNIQUEIDENTIFIER NOT NULL,  -- Polymorphic FK
    PhotoPurpose        NVARCHAR(50) NOT NULL,      -- Overview, Detail, Before, After, Defect, Verification
    SectionReference    NVARCHAR(100) NOT NULL,     -- "Roof", "Kitchen", "Engine", etc.
    Notes               NVARCHAR(MAX) NULL,
    LinkedBy            UNIQUEIDENTIFIER NOT NULL,
    LinkedAt            DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_PropertyPhotoMapping_Photo FOREIGN KEY (PhotoId)
        REFERENCES appraisal.GalleryPhotos(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PropertyPhotoMapping_LinkedBy FOREIGN KEY (LinkedBy)
        REFERENCES auth.Users(Id)
);

CREATE INDEX IX_PropertyPhotoMapping_PhotoId ON appraisal.PropertyPhotoMappings(PhotoId);
CREATE INDEX IX_PropertyPhotoMapping_PropertyDetail ON appraisal.PropertyPhotoMappings(PropertyDetailType, PropertyDetailId);
CREATE INDEX IX_PropertyPhotoMapping_SectionReference ON appraisal.PropertyPhotoMappings(SectionReference);
```

**Mapping Examples**:

```csharp
// Example 1: Kitchen photo mapped to multiple sections
// Photo shows both kitchen and flooring
await _mappingRepository.CreateAsync(new PropertyPhotoMapping
{
    PhotoId = kitchenPhotoId,
    PropertyDetailType = PropertyDetailType.Building,
    PropertyDetailId = buildingDetailId,
    PhotoPurpose = PhotoPurpose.Detail,
    SectionReference = "Kitchen",
    Notes = "Shows granite countertop and built-in cabinets",
    LinkedBy = appraiserId
});

await _mappingRepository.CreateAsync(new PropertyPhotoMapping
{
    PhotoId = kitchenPhotoId,  // Same photo
    PropertyDetailType = PropertyDetailType.Building,
    PropertyDetailId = buildingDetailId,
    PhotoPurpose = PhotoPurpose.Detail,
    SectionReference = "Flooring",
    Notes = "Ceramic tile flooring visible in foreground",
    LinkedBy = appraiserId
});

// Example 2: Roof defect photo
await _mappingRepository.CreateAsync(new PropertyPhotoMapping
{
    PhotoId = roofDefectPhotoId,
    PropertyDetailType = PropertyDetailType.Building,
    PropertyDetailId = buildingDetailId,
    PhotoPurpose = PhotoPurpose.Defect,
    SectionReference = "RoofStructure",
    Notes = "Damaged tiles on northeast corner, approximately 2 sqm affected",
    LinkedBy = appraiserId
});
```

**Querying Mapped Photos**:

```csharp
// Get all photos for building's roof section
var roofPhotos = await _context.PropertyPhotoMappings
    .Where(m => m.PropertyDetailId == buildingDetailId
             && m.SectionReference == "RoofStructure")
    .Include(m => m.Photo)
        .ThenInclude(p => p.Document)
    .ToListAsync();

// Get all defect photos for a property
var defectPhotos = await _context.PropertyPhotoMappings
    .Where(m => m.PropertyDetailId == buildingDetailId
             && m.PhotoPurpose == PhotoPurpose.Defect)
    .Include(m => m.Photo)
    .ToListAsync();
```

### 4. PhotoAnnotation

**Purpose**: Add visual annotations to highlight issues, measurements, or areas of interest

```sql
CREATE TABLE appraisal.PhotoAnnotations
(
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    PhotoId             UNIQUEIDENTIFIER NOT NULL,
    AnnotationType      NVARCHAR(50) NOT NULL,      -- Arrow, Circle, Rectangle, Text, Highlight, Measurement
    CoordinatesData     NVARCHAR(MAX) NOT NULL,     -- JSON: {x, y, width, height, points, etc.}
    AnnotationText      NVARCHAR(500) NULL,
    Color               NVARCHAR(20) NOT NULL,      -- Hex color code
    StrokeWidth         INT NOT NULL DEFAULT 2,
    IssueType           NVARCHAR(50) NULL,          -- Defect, Damage, Concern, Note, Measurement
    Severity            NVARCHAR(50) NULL,          -- Critical, Major, Minor, Info
    CreatedBy           UNIQUEIDENTIFIER NOT NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_PhotoAnnotation_Photo FOREIGN KEY (PhotoId)
        REFERENCES appraisal.GalleryPhotos(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PhotoAnnotation_CreatedBy FOREIGN KEY (CreatedBy)
        REFERENCES auth.Users(Id)
);

CREATE INDEX IX_PhotoAnnotation_PhotoId ON appraisal.PhotoAnnotations(PhotoId);
CREATE INDEX IX_PhotoAnnotation_IssueType ON appraisal.PhotoAnnotations(IssueType);
```

**Annotation Examples**:

```csharp
// Highlight crack with arrow
var annotation = new PhotoAnnotation
{
    PhotoId = wallPhotoId,
    AnnotationType = AnnotationType.Arrow,
    CoordinatesData = @"{
        ""startX"": 100,
        ""startY"": 150,
        ""endX"": 200,
        ""endY"": 250,
        ""arrowSize"": 10
    }",
    AnnotationText = "Structural crack - 5mm width",
    Color = "#FF0000",  // Red
    StrokeWidth = 3,
    IssueType = IssueType.Defect,
    Severity = Severity.Major,
    CreatedBy = appraiserId
};

// Circle to highlight water damage
var circleAnnotation = new PhotoAnnotation
{
    PhotoId = ceilingPhotoId,
    AnnotationType = AnnotationType.Circle,
    CoordinatesData = @"{
        ""centerX"": 500,
        ""centerY"": 300,
        ""radius"": 50
    }",
    AnnotationText = "Water stain - 30cm diameter",
    Color = "#FFA500",  // Orange
    IssueType = IssueType.Damage,
    Severity = Severity.Major
};

// Measurement line
var measurementAnnotation = new PhotoAnnotation
{
    AnnotationType = AnnotationType.Measurement,
    CoordinatesData = @"{
        ""points"": [
            {""x"": 100, ""y"": 200},
            {""x"": 400, ""y"": 200}
        ],
        ""length"": ""3.5m""
    }",
    AnnotationText = "Door width: 3.5 meters",
    Color = "#00FF00",  // Green
    IssueType = IssueType.Measurement
};
```

**Rendering Annotations**:

```typescript
// Frontend: Draw annotation on canvas/image
function drawAnnotation(annotation: PhotoAnnotation, canvas: HTMLCanvasElement) {
    const ctx = canvas.getContext('2d');
    const coords = JSON.parse(annotation.coordinatesData);

    ctx.strokeStyle = annotation.color;
    ctx.lineWidth = annotation.strokeWidth;

    switch (annotation.annotationType) {
        case 'Arrow':
            drawArrow(ctx, coords.startX, coords.startY, coords.endX, coords.endY);
            break;
        case 'Circle':
            ctx.beginPath();
            ctx.arc(coords.centerX, coords.centerY, coords.radius, 0, 2 * Math.PI);
            ctx.stroke();
            break;
        case 'Rectangle':
            ctx.strokeRect(coords.x, coords.y, coords.width, coords.height);
            break;
        // ... other types
    }

    // Draw text label
    if (annotation.annotationText) {
        ctx.fillStyle = annotation.color;
        ctx.fillText(annotation.annotationText, coords.x, coords.y - 10);
    }
}
```

### 5. PhotoComparisonSet

**Purpose**: Group photos for before/after or multi-angle comparisons

```sql
CREATE TABLE appraisal.PhotoComparisonSets
(
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AppraisalId         UNIQUEIDENTIFIER NOT NULL,
    ComparisonType      NVARCHAR(50) NOT NULL,      -- BeforeAfter, TimeSeries, MultiAngle, Comparative
    Title               NVARCHAR(200) NOT NULL,
    Description         NVARCHAR(MAX) NULL,
    CreatedBy           UNIQUEIDENTIFIER NOT NULL,
    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_PhotoComparisonSet_Appraisal FOREIGN KEY (AppraisalId)
        REFERENCES appraisal.Appraisals(Id) ON DELETE CASCADE
);

CREATE TABLE appraisal.PhotoComparisonItems
(
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ComparisonSetId     UNIQUEIDENTIFIER NOT NULL,
    PhotoId             UNIQUEIDENTIFIER NOT NULL,
    ItemLabel           NVARCHAR(100) NOT NULL,     -- "Before", "After", "View 1", etc.
    ItemDate            DATETIME2 NOT NULL,
    DisplayOrder        INT NOT NULL,
    Notes               NVARCHAR(MAX) NULL,

    CONSTRAINT FK_PhotoComparisonItem_Set FOREIGN KEY (ComparisonSetId)
        REFERENCES appraisal.PhotoComparisonSets(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PhotoComparisonItem_Photo FOREIGN KEY (PhotoId)
        REFERENCES appraisal.GalleryPhotos(Id)
);
```

**Comparison Examples**:

```csharp
// Before/After repair comparison
var comparisonSet = new PhotoComparisonSet
{
    AppraisalId = appraisalId,
    ComparisonType = ComparisonType.BeforeAfter,
    Title = "Roof Repair - Before and After",
    Description = "Comparison showing roof condition before and after repair work",
    CreatedBy = appraiserId
};

await _context.PhotoComparisonItems.AddRangeAsync(
    new PhotoComparisonItem
    {
        ComparisonSetId = comparisonSet.Id,
        PhotoId = beforePhotoId,
        ItemLabel = "Before Repair",
        ItemDate = new DateTime(2025, 1, 10),
        DisplayOrder = 1,
        Notes = "Multiple damaged tiles visible"
    },
    new PhotoComparisonItem
    {
        ComparisonSetId = comparisonSet.Id,
        PhotoId = afterPhotoId,
        ItemLabel = "After Repair",
        ItemDate = new DateTime(2025, 1, 20),
        DisplayOrder = 2,
        Notes = "All tiles replaced, roof restored to good condition"
    }
);
```

### 6. VideoRecording

**Purpose**: Store video walkthrough and demonstrations

```sql
CREATE TABLE appraisal.VideoRecordings
(
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AppraisalId         UNIQUEIDENTIFIER NOT NULL,
    DocumentId          UNIQUEIDENTIFIER NOT NULL,
    VideoTitle          NVARCHAR(200) NOT NULL,
    VideoType           NVARCHAR(50) NOT NULL,      -- Walkthrough, Demonstration, Interview, DroneFootage
    Duration            INT NOT NULL,               -- seconds
    FileSize            BIGINT NOT NULL,
    Resolution          NVARCHAR(20) NULL,          -- "1920x1080", "3840x2160"
    Format              NVARCHAR(20) NOT NULL,      -- "MP4", "MOV", "AVI"
    ThumbnailUrl        NVARCHAR(500) NOT NULL,
    VideoUrl            NVARCHAR(500) NOT NULL,

    -- Location
    Latitude            DECIMAL(10, 8) NULL,
    Longitude           DECIMAL(11, 8) NULL,
    LocationName        NVARCHAR(200) NULL,

    Description         NVARCHAR(MAX) NULL,
    Tags                NVARCHAR(MAX) NULL,         -- JSON array

    RecordedAt          DATETIME2 NOT NULL,
    RecordedBy          UNIQUEIDENTIFIER NOT NULL,
    UploadedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UploadedBy          UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_VideoRecording_Appraisal FOREIGN KEY (AppraisalId)
        REFERENCES appraisal.Appraisals(Id) ON DELETE CASCADE,
    CONSTRAINT FK_VideoRecording_Document FOREIGN KEY (DocumentId)
        REFERENCES document.Documents(Id)
);
```

**Video Examples**:

```csharp
// Property walkthrough video
var video = new VideoRecording
{
    AppraisalId = appraisalId,
    DocumentId = documentId,
    VideoTitle = "Complete Property Walkthrough",
    VideoType = VideoType.Walkthrough,
    Duration = 480,  // 8 minutes
    FileSize = 157286400,  // ~150 MB
    Resolution = "1920x1080",
    Format = "MP4",
    Description = "Comprehensive walkthrough covering all rooms and exterior",
    Tags = "[\"walkthrough\", \"interior\", \"exterior\"]",
    RecordedAt = DateTime.UtcNow,
    RecordedBy = appraiserId
};

// Machinery demonstration
var demoVideo = new VideoRecording
{
    VideoTitle = "CNC Machine Operational Test",
    VideoType = VideoType.Demonstration,
    Duration = 180,  // 3 minutes
    Description = "Demonstration of CNC machine operation, showing all axes movement and control panel functionality"
};
```

### 7. AudioNote

**Purpose**: Voice notes and audio recordings from site visits

```sql
CREATE TABLE appraisal.AudioNotes
(
    Id                      UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AppraisalId             UNIQUEIDENTIFIER NOT NULL,
    DocumentId              UNIQUEIDENTIFIER NOT NULL,
    AudioTitle              NVARCHAR(200) NOT NULL,
    AudioType               NVARCHAR(50) NOT NULL,  -- VoiceNote, Interview, Observation
    Duration                INT NOT NULL,           -- seconds
    FileSize                BIGINT NOT NULL,
    Format                  NVARCHAR(20) NOT NULL,  -- "MP3", "M4A", "WAV"
    AudioUrl                NVARCHAR(500) NOT NULL,

    -- Transcription
    TranscriptionText       NVARCHAR(MAX) NULL,
    TranscriptionStatus     NVARCHAR(50) NOT NULL,  -- None, InProgress, Completed, Failed

    RelatedPhotoId          UNIQUEIDENTIFIER NULL,  -- Optional link to photo
    Description             NVARCHAR(MAX) NULL,

    RecordedAt              DATETIME2 NOT NULL,
    RecordedBy              UNIQUEIDENTIFIER NOT NULL,
    UploadedAt              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_AudioNote_Appraisal FOREIGN KEY (AppraisalId)
        REFERENCES appraisal.Appraisals(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AudioNote_Document FOREIGN KEY (DocumentId)
        REFERENCES document.Documents(Id),
    CONSTRAINT FK_AudioNote_RelatedPhoto FOREIGN KEY (RelatedPhotoId)
        REFERENCES appraisal.GalleryPhotos(Id)
);
```

**Audio Examples**:

```csharp
// Quick voice note about defect
var audioNote = new AudioNote
{
    AppraisalId = appraisalId,
    DocumentId = documentId,
    AudioTitle = "Roof Damage Observation",
    AudioType = AudioType.Observation,
    Duration = 45,  // 45 seconds
    Format = "M4A",
    Description = "Quick note about water damage extent",
    RelatedPhotoId = roofPhotoId,  // Link to related photo
    TranscriptionStatus = TranscriptionStatus.InProgress,
    RecordedAt = DateTime.UtcNow,
    RecordedBy = appraiserId
};

// Auto-transcription result (after processing)
audioNote.TranscriptionText = @"
    The roof shows significant water damage on the northeast corner.
    Approximately 2 square meters of tiles need replacement.
    The underlying structure appears sound, but recommend inspection
    after tile removal to confirm no structural damage.
";
audioNote.TranscriptionStatus = TranscriptionStatus.Completed;
```

### 8. UploadSession

**Purpose**: Track batch uploads and monitor progress

```sql
CREATE TABLE appraisal.UploadSessions
(
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    AppraisalId         UNIQUEIDENTIFIER NOT NULL,
    SessionType         NVARCHAR(50) NOT NULL,      -- SiteVisit, OfficeUpload, MobileUpload, BulkUpload
    SessionName         NVARCHAR(200) NOT NULL,
    UploadMethod        NVARCHAR(50) NOT NULL,      -- Mobile, Web, API, Sync

    -- Device Information (Value Object)
    DeviceType          NVARCHAR(50) NULL,          -- iOS, Android, Web, Desktop
    DeviceModel         NVARCHAR(100) NULL,
    OSVersion           NVARCHAR(50) NULL,
    AppVersion          NVARCHAR(50) NULL,
    DeviceId            NVARCHAR(200) NULL,

    -- Statistics
    TotalPhotos         INT NOT NULL DEFAULT 0,
    TotalVideos         INT NOT NULL DEFAULT 0,
    TotalSize           BIGINT NOT NULL DEFAULT 0,  -- bytes

    -- Timestamps
    UploadStarted       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UploadCompleted     DATETIME2 NULL,
    Status              NVARCHAR(50) NOT NULL,      -- InProgress, Completed, Failed, Cancelled

    -- Location
    Latitude            DECIMAL(10, 8) NULL,
    Longitude           DECIMAL(11, 8) NULL,

    UploadedBy          UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT FK_UploadSession_Appraisal FOREIGN KEY (AppraisalId)
        REFERENCES appraisal.Appraisals(Id) ON DELETE CASCADE
);
```

**Session Tracking**:

```csharp
// Start upload session
var session = new UploadSession
{
    AppraisalId = appraisalId,
    SessionType = SessionType.SiteVisit,
    SessionName = "Site Visit - 123 Main St",
    UploadMethod = UploadMethod.Mobile,
    DeviceType = DeviceType.iOS,
    DeviceModel = "iPhone 15 Pro",
    OSVersion = "iOS 17.2",
    AppVersion = "2.5.0",
    Status = UploadStatus.InProgress,
    UploadedBy = appraiserId
};

// Update as photos upload
session.TotalPhotos++;
session.TotalSize += photoFileSize;

// Complete session
session.UploadCompleted = DateTime.UtcNow;
session.Status = UploadStatus.Completed;
```

### 9. MediaProcessingJob

**Purpose**: Background processing for thumbnails, compression, transcription

```sql
CREATE TABLE appraisal.MediaProcessingJobs
(
    Id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    MediaId             UNIQUEIDENTIFIER NOT NULL,
    MediaType           NVARCHAR(50) NOT NULL,      -- Photo, Video, Audio
    JobType             NVARCHAR(50) NOT NULL,      -- ThumbnailGeneration, Compression, Transcription, Watermark, OCR
    Status              NVARCHAR(50) NOT NULL,      -- Pending, Processing, Completed, Failed
    Priority            NVARCHAR(50) NOT NULL,      -- Low, Normal, High

    StartedAt           DATETIME2 NULL,
    CompletedAt         DATETIME2 NULL,
    ErrorMessage        NVARCHAR(MAX) NULL,
    ResultData          NVARCHAR(MAX) NULL,         -- JSON result

    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           UNIQUEIDENTIFIER NOT NULL
);

CREATE INDEX IX_MediaProcessingJob_Status ON appraisal.MediaProcessingJobs(Status) WHERE Status IN ('Pending', 'Processing');
CREATE INDEX IX_MediaProcessingJob_MediaId ON appraisal.MediaProcessingJobs(MediaId);
```

**Processing Pipeline**:

```csharp
// Photo uploaded → Create processing jobs
var jobs = new List<MediaProcessingJob>
{
    new MediaProcessingJob
    {
        MediaId = photoId,
        MediaType = MediaType.Photo,
        JobType = JobType.ThumbnailGeneration,
        Status = JobStatus.Pending,
        Priority = Priority.High,  // Thumbnails needed immediately
        CreatedBy = systemUserId
    },
    new MediaProcessingJob
    {
        MediaId = photoId,
        MediaType = MediaType.Photo,
        JobType = JobType.Compression,
        Status = JobStatus.Pending,
        Priority = Priority.Normal,
        CreatedBy = systemUserId
    },
    new MediaProcessingJob
    {
        MediaId = photoId,
        MediaType = MediaType.Photo,
        JobType = JobType.Watermark,
        Status = JobStatus.Pending,
        Priority = Priority.Low,
        CreatedBy = systemUserId
    }
};

// Background worker processes jobs by priority
async Task ProcessJobAsync(MediaProcessingJob job)
{
    job.Status = JobStatus.Processing;
    job.StartedAt = DateTime.UtcNow;

    try
    {
        switch (job.JobType)
        {
            case JobType.ThumbnailGeneration:
                var thumbnailUrl = await _imageService.GenerateThumbnailAsync(job.MediaId);
                job.ResultData = JsonSerializer.Serialize(new { ThumbnailUrl = thumbnailUrl });
                break;

            case JobType.Compression:
                var compressedSize = await _imageService.CompressAsync(job.MediaId);
                job.ResultData = JsonSerializer.Serialize(new { OriginalSize = originalSize, CompressedSize = compressedSize });
                break;

            case JobType.Transcription:
                var text = await _audioService.TranscribeAsync(job.MediaId);
                job.ResultData = JsonSerializer.Serialize(new { TranscriptionText = text });
                break;
        }

        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
    }
    catch (Exception ex)
    {
        job.Status = JobStatus.Failed;
        job.ErrorMessage = ex.Message;
        job.CompletedAt = DateTime.UtcNow;
    }
}
```

## Workflow Implementation

### Phase 1: Mobile Upload Flow

```csharp
public class MobilePhotoUploadService
{
    public async Task<Result<Guid>> UploadPhotoAsync(
        Guid appraisalId,
        Stream photoStream,
        PhotoMetadata metadata,
        CancellationToken cancellationToken)
    {
        // 1. Create or get gallery for this session
        var gallery = await GetOrCreateGalleryAsync(appraisalId, metadata.CapturedAt);

        // 2. Upload to document storage
        var document = await _documentService.UploadAsync(
            photoStream,
            metadata.FileName,
            DocumentCategory.Appraisal,
            cancellationToken);

        // 3. Create gallery photo record
        var photo = new GalleryPhoto
        {
            GalleryId = gallery.Id,
            DocumentId = document.Id,
            PhotoNumber = await GetNextPhotoNumberAsync(gallery.Id),
            DisplayOrder = metadata.DisplayOrder,
            PhotoType = metadata.PhotoType,
            PhotoCategory = metadata.PhotoCategory,
            Latitude = metadata.Latitude,
            Longitude = metadata.Longitude,
            CapturedAt = metadata.CapturedAt,
            CapturedBy = _currentUserService.UserId,
            DeviceModel = metadata.DeviceModel,
            FullSizeUrl = document.StorageLocation,
            FileSize = metadata.FileSize,
            Width = metadata.Width,
            Height = metadata.Height,
            UploadedBy = _currentUserService.UserId
        };

        await _photoRepository.CreateAsync(photo, cancellationToken);

        // 4. Queue background processing
        await QueueMediaProcessingJobsAsync(photo.Id);

        // 5. Update upload session statistics
        await UpdateUploadSessionAsync(metadata.SessionId, photo);

        return Result.Success(photo.Id);
    }

    private async Task QueueMediaProcessingJobsAsync(Guid photoId)
    {
        var jobs = new[]
        {
            new MediaProcessingJob { MediaId = photoId, JobType = JobType.ThumbnailGeneration, Priority = Priority.High },
            new MediaProcessingJob { MediaId = photoId, JobType = JobType.Compression, Priority = Priority.Normal },
            new MediaProcessingJob { MediaId = photoId, JobType = JobType.Watermark, Priority = Priority.Low }
        };

        await _jobRepository.CreateBatchAsync(jobs);
    }
}
```

### Phase 2: Photo Linking Flow

```csharp
public class PropertyPhotoLinkingService
{
    public async Task<Result> LinkPhotoToPropertySectionAsync(
        Guid photoId,
        Guid propertyDetailId,
        PropertyDetailType detailType,
        string sectionReference,
        PhotoPurpose purpose,
        string? notes,
        CancellationToken cancellationToken)
    {
        // 1. Validate photo exists and belongs to same appraisal
        var photo = await _photoRepository.GetByIdAsync(photoId, cancellationToken);
        if (photo == null)
            return Result.Failure("Photo not found");

        // 2. Validate property detail exists
        var propertyDetail = await _propertyDetailRepository.GetByIdAsync(
            propertyDetailId,
            detailType,
            cancellationToken);
        if (propertyDetail == null)
            return Result.Failure("Property detail not found");

        // 3. Verify photo and property belong to same appraisal
        if (photo.Gallery.AppraisalId != propertyDetail.AppraisalId)
            return Result.Failure("Photo and property detail belong to different appraisals");

        // 4. Create mapping
        var mapping = new PropertyPhotoMapping
        {
            PhotoId = photoId,
            PropertyDetailType = detailType,
            PropertyDetailId = propertyDetailId,
            PhotoPurpose = purpose,
            SectionReference = sectionReference,
            Notes = notes,
            LinkedBy = _currentUserService.UserId
        };

        await _mappingRepository.CreateAsync(mapping, cancellationToken);

        // 5. Mark photo as used
        photo.IsSelected = true;
        await _photoRepository.UpdateAsync(photo, cancellationToken);

        return Result.Success();
    }

    // Bulk linking for multiple photos
    public async Task<Result> LinkMultiplePhotosAsync(
        IEnumerable<Guid> photoIds,
        Guid propertyDetailId,
        PropertyDetailType detailType,
        string sectionReference,
        CancellationToken cancellationToken)
    {
        var mappings = photoIds.Select(photoId => new PropertyPhotoMapping
        {
            PhotoId = photoId,
            PropertyDetailType = detailType,
            PropertyDetailId = propertyDetailId,
            PhotoPurpose = PhotoPurpose.Detail,
            SectionReference = sectionReference,
            LinkedBy = _currentUserService.UserId
        });

        await _mappingRepository.CreateBatchAsync(mappings, cancellationToken);

        return Result.Success();
    }
}
```

### Querying Photos for Property Detail

```csharp
public class PropertyPhotoQueryService
{
    // Get all photos for a property detail
    public async Task<IEnumerable<GalleryPhotoDto>> GetPhotosForPropertyAsync(
        Guid propertyDetailId,
        PropertyDetailType detailType,
        CancellationToken cancellationToken)
    {
        return await _context.PropertyPhotoMappings
            .Where(m => m.PropertyDetailId == propertyDetailId
                     && m.PropertyDetailType == detailType)
            .Include(m => m.Photo)
                .ThenInclude(p => p.Document)
            .Include(m => m.Photo)
                .ThenInclude(p => p.Annotations)
            .Select(m => new GalleryPhotoDto
            {
                PhotoId = m.Photo.Id,
                ThumbnailUrl = m.Photo.ThumbnailUrl,
                FullSizeUrl = m.Photo.FullSizeUrl,
                Caption = m.Photo.Caption,
                PhotoType = m.Photo.PhotoType,
                PhotoCategory = m.Photo.PhotoCategory,
                SectionReference = m.SectionReference,
                PhotoPurpose = m.PhotoPurpose,
                MappingNotes = m.Notes,
                AnnotationCount = m.Photo.Annotations.Count,
                CapturedAt = m.Photo.CapturedAt
            })
            .OrderBy(p => p.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    // Get photos by section
    public async Task<IEnumerable<GalleryPhotoDto>> GetPhotosBySectionAsync(
        Guid propertyDetailId,
        string sectionReference,
        CancellationToken cancellationToken)
    {
        return await _context.PropertyPhotoMappings
            .Where(m => m.PropertyDetailId == propertyDetailId
                     && m.SectionReference == sectionReference)
            .Include(m => m.Photo)
            .Select(m => MapToDto(m.Photo, m))
            .ToListAsync(cancellationToken);
    }

    // Get defect photos
    public async Task<IEnumerable<DefectPhotoDto>> GetDefectPhotosAsync(
        Guid propertyDetailId,
        CancellationToken cancellationToken)
    {
        return await _context.PropertyPhotoMappings
            .Where(m => m.PropertyDetailId == propertyDetailId
                     && m.PhotoPurpose == PhotoPurpose.Defect)
            .Include(m => m.Photo)
                .ThenInclude(p => p.Annotations.Where(a => a.IssueType == IssueType.Defect))
            .Select(m => new DefectPhotoDto
            {
                PhotoId = m.Photo.Id,
                ThumbnailUrl = m.Photo.ThumbnailUrl,
                SectionReference = m.SectionReference,
                Description = m.Notes,
                Severity = m.Photo.Annotations
                    .Where(a => a.Severity != null)
                    .OrderByDescending(a => a.Severity)
                    .Select(a => a.Severity)
                    .FirstOrDefault(),
                AnnotationCount = m.Photo.Annotations.Count
            })
            .ToListAsync(cancellationToken);
    }

    // Get photos for report
    public async Task<IEnumerable<ReportPhotoDto>> GetReportPhotosAsync(
        Guid appraisalId,
        CancellationToken cancellationToken)
    {
        return await _context.GalleryPhotos
            .Where(p => p.Gallery.AppraisalId == appraisalId
                     && p.IsUsedInReport)
            .Include(p => p.Mappings)
            .OrderBy(p => p.PhotoType)
                .ThenBy(p => p.DisplayOrder)
            .Select(p => new ReportPhotoDto
            {
                PhotoId = p.Id,
                FullSizeUrl = p.FullSizeUrl,
                Caption = p.Caption,
                Description = p.Description,
                PhotoType = p.PhotoType,
                Sections = p.Mappings.Select(m => m.SectionReference).ToList()
            })
            .ToListAsync(cancellationToken);
    }
}
```

## Best Practices

### 1. Photo Organization
- Create new gallery for each site visit session
- Use consistent naming: "Site Visit - {Date} {Time}"
- Categorize photos during upload for easier finding later

### 2. Storage Optimization
- Generate thumbnails immediately (high priority)
- Compress full-size images in background
- Store thumbnails in fast storage (CDN)
- Archive old photos to cold storage

### 3. Performance
- Lazy load full-size images
- Show thumbnails by default
- Paginate photo galleries
- Cache frequently accessed photos

### 4. Mobile Considerations
- Queue uploads when offline
- Resume interrupted uploads
- Compress before upload to save bandwidth
- Show upload progress clearly

### 5. Photo Linking
- Allow multiple photos per section
- Allow one photo to map to multiple sections
- Provide section dropdown based on property type
- Show preview when linking

### 6. Annotations
- Save annotation data as JSON for flexibility
- Support undo/redo
- Allow annotation editing
- Export annotations with report

### 7. Security
- Watermark photos with company logo
- Control access via DocumentAccess table
- Audit photo views and downloads
- Encrypt sensitive property photos

---

**Next**: See [10-er-diagrams.md](10-er-diagrams.md) for visual entity relationships.
