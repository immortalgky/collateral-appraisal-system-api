using Appraisal.Application.Features.Appraisals.RemoveGalleryPhoto;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using NSubstitute;
using Shared.Data.Outbox;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Deleting a gallery photo must also unlink it from Block towers and models (they hold the photo id with no
/// foreign key) and hand the cover to the next image, so the next photo added can still become the cover.
/// </summary>
public class RemoveGalleryPhotoCommandHandlerTests
{
    private readonly IAppraisalGalleryRepository _gallery = Substitute.For<IAppraisalGalleryRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();

    [Fact]
    public async Task Handle_UnlinksTowerAndModelImagesAndPromotesTheNextCover()
    {
        var photo = AppraisalGallery.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Photo", "EMP001");
        var other = Guid.NewGuid();
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        var tower = project.AddTower();
        tower.AddImage(photo.Id); // cover
        tower.AddImage(other);
        var model = project.AddModel(tower.Id, "M");
        model.AddImage(photo.Id); // cover
        model.AddImage(other);

        _gallery.GetByIdAsync(photo.Id, Arg.Any<CancellationToken>()).Returns(photo);
        _gallery.GetMappingsByPhotoIdAsync(photo.Id, Arg.Any<CancellationToken>()).Returns([]);
        _projects.GetByGalleryPhotoAsync(photo.Id, Arg.Any<CancellationToken>()).Returns([project]);

        await new RemoveGalleryPhotoCommandHandler(_gallery, _projects, _outbox)
            .Handle(new RemoveGalleryPhotoCommand(photo.Id), CancellationToken.None);

        var towerImage = Assert.Single(tower.Images);
        Assert.Equal(other, towerImage.GalleryPhotoId);
        Assert.True(towerImage.IsThumbnail);
        var modelImage = Assert.Single(model.Images);
        Assert.Equal(other, modelImage.GalleryPhotoId);
        Assert.True(modelImage.IsThumbnail);
        await _gallery.Received(1).DeleteAsync(photo, Arg.Any<CancellationToken>());
    }
}
