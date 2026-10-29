using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CreateLandProperty;
using Appraisal.Application.Features.Appraisals.GetAppraisalBrief;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared.Data;
using Shared.Identity;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// The brief is one hand-written Dapper batch of 20 result sets, and no unit test executes it. The two
/// big rows (AssetRow, ProjectRow) bind BY NAME: a SELECT alias that no longer matches a property does
/// not throw, it just leaves that property null, so a renamed alias would silently blank a field. This
/// runs the whole batch against the real migrated schema (views and masters included), with one
/// appraisal per branch the batch has (property-based, condo project, horizontal project), and asserts
/// a handful of seeded descriptive values so that a renamed alias fails here.
/// </summary>
[Collection("Integration")]
public class GetAppraisalBriefQueryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Through the mediator rather than the HTTP route: the route needs the "appraisal.browse" policy,
    // which this harness's anonymous client does not hold, and the batch is what is under test.
    private async Task<JsonElement> GetBriefAsync(Guid appraisalId)
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetAppraisalBriefQuery(appraisalId), Ct);
        return JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    // A Thai label the brief should resolve for a code that exists only in this test, so the label result
    // set and its mapping are what produce the text, not a master row that happens to be seeded.
    private async Task SeedLabelAsync(string group, string code, string description)
    {
        await using var conn = new SqlConnection(Fixture.ConnectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM parameter.Parameters WHERE [Group] = @group AND Code = @code AND [Language] = N'TH')
                INSERT parameter.Parameters ([Group], Country, [Language], Code, Description, IsActive, SeqNo)
                VALUES (@group, N'TH', N'TH', @code, @description, 1, 1)
            """,
            new { group, code, description }, cancellationToken: Ct));
    }

    [Fact]
    public async Task A_property_based_appraisal_returns_titles_condo_building_and_machine_detail()
    {
        await SeedLabelAsync("BuildingType", "ZB", "Test Building Type");
        Guid appraisalId;
        using (var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"BRF-{Guid.NewGuid():N}"[..18]);

            var land = appraisal.AddLandProperty();
            LandDetailSync.SyncTitles(land.LandDetail!,
                [new LandTitleItemData(null, "T1", "DEED", Rai: 1m), new LandTitleItemData(null, "T2", "NS3", Rai: 2m)]);
            var building = appraisal.AddBuildingProperty();
            building.BuildingDetail!.Update(ownerName: "Owner", buildingType: "ZB", roofType: ["01"]);
            // 30% done: the progress query's row, bound by name.
            building.SetConstructionInspection(ConstructionInspection.CreateSummary(
                building.Id, 1_000m, null, null, 30m, null));
            building.BuildingDetail.AddDepreciationDetail("Gross", isBuilding: false, priceAfterDepreciation: 1m);
            var condo = appraisal.AddCondoProperty();
            condo.CondoDetail!.Update(ownerName: "Owner", roomNumber: "9/9");
            condo.CondoDetail.AddCondoAreaDetail(CondoAppraisalAreaDetail.Create(1, "Room", 33m));
            var machine = appraisal.AddMachineryProperty();
            machine.MachineryDetail!.Update(propertyName: "Crane", brand: "Acme", registrationStatus: true);

            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
        }

        var brief = await GetBriefAsync(appraisalId);

        Assert.Equal(4, brief.GetProperty("assets").GetArrayLength());
        var titles = brief.GetProperty("assets").EnumerateArray()
            .SelectMany(a => a.GetProperty("landTitles").EnumerateArray()).ToList();
        Assert.Equal(["T1", "T2"], titles.Select(t => t.GetProperty("titleNumber").GetString()));
        var listed = Assert.Single(brief.GetProperty("machines").EnumerateArray());
        Assert.Equal("Crane", listed.GetProperty("machineName").GetString());
        Assert.Equal("Acme", listed.GetProperty("brand").GetString());
        Assert.Equal("registered", listed.GetProperty("section").GetString());

        // Name-bound AssetRow columns: a roof code, a condo room number.
        var assets = brief.GetProperty("assets").EnumerateArray().ToList();
        Assert.Contains(assets, a => a.GetProperty("roofType").EnumerateArray().Any(c => c.GetString() == "01"));
        Assert.Contains(assets, a => a.GetProperty("roomNumber").GetString() == "9/9");

        // One assertion per remaining side result set: the inspection (ProgressRow), the condo area rows
        // (CondoAreaRow) and a parameter label (ParamLabelRow, resolved through LabelMap).
        Assert.Contains(assets, a => a.GetProperty("constructionProgressPercent").ValueKind == JsonValueKind.Number
                                     && a.GetProperty("constructionProgressPercent").GetDecimal() == 30m);
        var area = Assert.Single(assets.SelectMany(a => a.GetProperty("condoAreas").EnumerateArray()));
        Assert.Equal("Room", area.GetProperty("description").GetString());
        Assert.Equal(33m, area.GetProperty("size").GetDecimal());
        Assert.Contains(assets, a => a.GetProperty("buildingDisplayName").GetString() == "Test Building Type");

        // Standard body: D043, named from the DocumentTypes master by the last result set.
        Assert.Equal("D043", brief.GetProperty("summaryTypeCode").GetString());
        Assert.False(string.IsNullOrEmpty(brief.GetProperty("summaryTypeName").GetString()));
    }

    [Fact]
    public async Task A_progressive_appraisal_files_its_summary_as_D042()
    {
        Guid appraisalId;
        using (var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Progressive", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"BRG-{Guid.NewGuid():N}"[..18]);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
        }

        var brief = await GetBriefAsync(appraisalId);

        Assert.Equal("D042", brief.GetProperty("summaryTypeCode").GetString());
        Assert.False(string.IsNullOrEmpty(brief.GetProperty("summaryTypeName").GetString()));
    }

    [Theory]
    [InlineData(true)]    // condo project with a tower
    [InlineData(false)]   // horizontal (land and building) project with a house model
    public async Task A_block_appraisal_returns_its_project_detail(bool condo)
    {
        await SeedLabelAsync("PublicUtility", "ZU", "Test Utility");
        Guid appraisalId;
        using (var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Progressive", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"BRP-{Guid.NewGuid():N}"[..18]);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);

            var project = Project.Create(
                appraisal.Id, condo ? ProjectType.Condo : ProjectType.LandAndBuilding,
                projectName: "Brief Test", developer: "Dev", utilities: ["ZU"], facilities: ["01"]);
            if (condo)
            {
                project.AddTower("A");
                project.ImportUnits("u.xlsx", null,
                    [ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, 1, "A", "9/1", "101", "T1", 30m, 1m)]);
            }
            else
            {
                project.AddModel(null, "Monaco");
                project.GetOrCreateLand().Update(ownerName: "Owner", roadFrontage: 12m);   // ProjectLandRow
                project.ImportUnits("u.xlsx", null,
                    [ProjectUnit.CreateLandAndBuilding(project.Id, Guid.Empty, 1, "1", "9/1", "Monaco", 2, 50m, 120m, 1m)]);
            }
            db.Projects.Add(project);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
        }

        var brief = await GetBriefAsync(appraisalId);

        var p = brief.GetProperty("project");
        // Block wins over the appraisal type.
        Assert.Equal("D043", brief.GetProperty("summaryTypeCode").GetString());
        Assert.Equal("Brief Test", p.GetProperty("projectName").GetString());
        Assert.Equal("Dev", p.GetProperty("developer").GetString());   // ProjectRow, bound by name
        Assert.Equal(1, p.GetProperty("unitCount").GetInt32());
        Assert.Equal(["Test Utility"], p.GetProperty("utilities").EnumerateArray().Select(u => u.GetString()));
        if (!condo) Assert.Equal(12m, p.GetProperty("land").GetProperty("roadFrontage").GetDecimal());
        Assert.Equal(condo ? 1 : 0, p.GetProperty("towers").GetArrayLength());
        Assert.Equal(condo ? 0 : 1, p.GetProperty("houseModels").GetArrayLength());
    }

    // One appraisal whose request carries a request-level file (a D036 carried from a previous appraisal), a
    // title-level file and a request file under a code the master does not know, plus one file in the
    // appraisal's own folder. Returns the appraisal id and the id of that appraisal-folder file.
    private async Task<(Guid AppraisalId, Guid AppraisalFileId)> SeedAppraisalWithEveryDocumentSourceAsync()
    {
        var requestId = Guid.CreateVersion7();
        var titleId = Guid.CreateVersion7();
        var prevNumber = $"PV{Guid.NewGuid():N}"[..12];
        var requestFile = Guid.CreateVersion7();
        var unknownTypeFile = Guid.CreateVersion7();
        var titleFile = Guid.CreateVersion7();
        var vehicleId = Guid.CreateVersion7();
        var vehicleFile = Guid.CreateVersion7();
        var appraisalFile = Guid.CreateVersion7();

        Guid appraisalId;
        using (var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(requestId, "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"BRD-{Guid.NewGuid():N}"[..18]);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);
            appraisalId = appraisal.Id;
        }

        await using var conn = new SqlConnection(Fixture.ConnectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO request.Requests (Id, Status, Requestor, RequestorName, Creator, CreatorName, CreatedAt, IsPma, IsDeleted,
                                          Purpose, RequestNumber, RequestedAt)
            VALUES (@requestId, N'New', N'u', N'u', N'u', N'u', SYSDATETIME(), 0, 0, N'01', @requestNumber, SYSDATETIME());
            INSERT INTO request.RequestDetails (RequestId, HasAppraisalBook, PrevAppraisalNumber)
            VALUES (@requestId, 0, @prevNumber);
            INSERT INTO request.RequestTitles (Id, RequestId, CollateralType, TitleNumber, SequenceNumber)
            VALUES (@titleId, @requestId, N'02', N'LT-BRIEF-1', 1);
            -- A vehicle has no deed number: its plate is the number.
            INSERT INTO request.RequestTitles (Id, RequestId, CollateralType, LicensePlateNumber, SequenceNumber)
            VALUES (@vehicleId, @requestId, N'10', N'1กข-4321', 2);

            DECLARE @session uniqueidentifier = NEWID();
            INSERT document.UploadSessions (Id, Status, ExpiresAt, TotalDocuments, TotalSizeBytes)
            VALUES (@session, N'Completed', DATEADD(DAY, 1, SYSDATETIME()), 5, 6170);

            INSERT document.Documents (Id, UploadSessionId, DocumentType, DocumentCategory, FileName, FileExtension, FileSizeBytes,
                                       MimeType, StoragePath, StorageUrl, UploadedBy, UploadedByName, UploadedAt, ReferenceCount,
                                       IsOrphaned, AccessLevel, IsActive, IsArchived, IsDeleted)
            SELECT v.Id, @session, N'X', N'X', v.FileName, N'.pdf', 1234, N'application/pdf', N'p', N'u', N'u', N'u',
                   SYSDATETIME(), 1, 0, N'Public', 1, 0, 0
            FROM (VALUES (@requestFile, N'prev-summary.pdf'), (@unknownTypeFile, N'odd.pdf'),
                         (@titleFile, N'deed.pdf'), (@vehicleFile, N'registration.pdf'), (@appraisalFile, N'report.pdf')) v(Id, FileName);

            INSERT request.RequestDocuments (Id, RequestId, DocumentType, DocumentId, FileName, Source, IsRequired, UploadedAt)
            VALUES (NEWID(), @requestId, N'D036', @requestFile, N'prev-summary.pdf', N'PREV', 0, SYSDATETIME()),
                   (NEWID(), @requestId, N'ZZ99', @unknownTypeFile, N'odd.pdf', N'REQUEST', 0, SYSDATETIME());
            INSERT request.RequestTitleDocuments (Id, TitleId, DocumentType, DocumentId, FileName, Source, [Set], IsRequired, UploadedAt)
            VALUES (NEWID(), @titleId, N'D013', @titleFile, N'deed.pdf', N'REQUEST', 1, 0, SYSDATETIME()),
                   (NEWID(), @vehicleId, N'D013', @vehicleFile, N'registration.pdf', N'REQUEST', 1, 0, SYSDATETIME());
            INSERT appraisal.AppraisalDocuments (Id, AppraisalId, DocumentTypeCode, DocumentId, FileName, SortOrder)
            VALUES (NEWID(), @appraisalId, N'D001', @appraisalFile, N'report.pdf', 1);
            """,
            new
            {
                requestId, titleId, vehicleId, vehicleFile, prevNumber, appraisalId, requestFile, unknownTypeFile, titleFile, appraisalFile,
                requestNumber = $"RQ{Guid.NewGuid():N}"[..14]
            },
            cancellationToken: Ct));
        return (appraisalId, appraisalFile);
    }

    [Fact]
    public async Task The_documents_list_carries_the_appraisal_request_and_title_files_with_their_source()
    {
        var (appraisalId, appraisalFile) = await SeedAppraisalWithEveryDocumentSourceAsync();

        var brief = await GetBriefAsync(appraisalId);

        var docs = brief.GetProperty("documents").EnumerateArray().ToList();
        // Appraisal folder first, then the request's own files, then the collateral's. The literals are the
        // SQL's; the handler's release gate compares against its own consts, so this pins the two together:
        // a released caller must get the appraisal-folder file back as "Appraisal".
        Assert.Equal(["Appraisal", "Request", "Request", "Title", "Title"], docs.Select(d => d.GetProperty("source").GetString()));

        var appraisal = docs[0];
        Assert.Equal(appraisalFile, appraisal.GetProperty("documentId").GetGuid());
        Assert.True(appraisal.GetProperty("isPrimary").GetBoolean());

        // D036 is listed like any other code, and says which appraisal it came from.
        var carried = docs.Single(d => d.GetProperty("typeCode").GetString() == "D036");
        Assert.Equal("Request", carried.GetProperty("source").GetString());
        Assert.False(carried.GetProperty("isPrimary").GetBoolean());
        Assert.StartsWith("PV", carried.GetProperty("carriedFromAppraisalNumber").GetString());
        Assert.Equal(JsonValueKind.Null, carried.GetProperty("titleNumber").ValueKind);

        // A code the master does not know still shows, under its own code with no names.
        var unknown = docs.Single(d => d.GetProperty("typeCode").GetString() == "ZZ99");
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("typeName").ValueKind);
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("carriedFromAppraisalNumber").ValueKind);

        var title = docs[^2];
        Assert.Equal("D013", title.GetProperty("typeCode").GetString());
        Assert.Equal("LT-BRIEF-1", title.GetProperty("titleNumber").GetString());
        Assert.Equal("ที่ดินพร้อมสิ่งปลูกสร้าง", title.GetProperty("collateralTypeName").GetString());
        Assert.Equal(JsonValueKind.Null, title.GetProperty("carriedFromAppraisalNumber").ValueKind);
        Assert.Equal("deed.pdf", title.GetProperty("fileName").GetString());
        Assert.Equal(1234, title.GetProperty("fileSizeBytes").GetInt64());

        // Per-type number: the plate stands in for the deed number, and the Thai type label comes from the master.
        var vehicle = docs[^1];
        Assert.Equal("1กข-4321", vehicle.GetProperty("titleNumber").GetString());
        Assert.Equal("registration.pdf", vehicle.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task A_locked_tracking_only_caller_gets_the_request_and_title_files_but_no_appraisal_file()
    {
        var (appraisalId, appraisalFile) = await SeedAppraisalWithEveryDocumentSourceAsync();

        // The handler built by hand so the caller can be tracking-only: the harness's own caller holds neither permission.
        var user = Substitute.For<ICurrentUserService>();
        user.HasPermission("APPRAISAL_TRACKING_VIEW").Returns(true);
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var handler = new GetAppraisalBriefQueryHandler(
            scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>(), user);
        var result = await handler.Handle(new GetAppraisalBriefQuery(appraisalId), Ct);

        Assert.False(result.DocumentsReleased);
        Assert.Equal(["Request", "Request", "Title", "Title"], result.Documents.Select(d => d.Source));
        // Not merely hidden: the appraisal file's id must not leave the handler at all.
        Assert.DoesNotContain(result.Documents, d => d.DocumentId == appraisalFile);
        Assert.DoesNotContain(result.Documents, d => d.IsPrimary);
    }
}
