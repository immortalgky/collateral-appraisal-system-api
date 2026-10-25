using Appraisal.Domain.Projects;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Deleting a gallery photo takes the tower / model images that show it away. When the cover goes, the
/// image with the lowest DisplaySequence becomes the cover; a dangling cover row used to stop the next
/// photo added from becoming one.
/// </summary>
public class ProjectImageRemovalByPhotoTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static ProjectTower Tower() => Project.Create(Guid.NewGuid(), ProjectType.Condo).AddTower();

    private static ProjectModel Model()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        return project.AddModel(project.AddTower().Id, "M");
    }

    [Fact]
    public void Tower_RemovingTheCoverPhoto_PromotesTheLowestSequenceImage()
    {
        var tower = Tower();
        tower.AddImage(A); // seq 1, cover
        tower.AddImage(B); // seq 2
        tower.AddImage(C); // seq 3

        tower.RemoveImagesOfPhoto(A);

        Assert.Equal([B, C], tower.Images.Select(i => i.GalleryPhotoId));
        Assert.Equal(B, Assert.Single(tower.Images, i => i.IsThumbnail).GalleryPhotoId);
    }

    [Fact]
    public void Tower_RemovingANonCoverPhoto_KeepsTheCover()
    {
        var tower = Tower();
        tower.AddImage(A);
        tower.AddImage(B);

        tower.RemoveImagesOfPhoto(B);

        Assert.Equal(A, Assert.Single(tower.Images).GalleryPhotoId);
        Assert.True(tower.Images[0].IsThumbnail);
    }

    [Fact]
    public void Tower_RemovingAPhotoWhenCoverWasUnset_DoesNotInventACover()
    {
        var tower = Tower();
        tower.AddImage(A);
        tower.AddImage(B);
        tower.UnsetThumbnail(tower.Images[0].Id);

        tower.RemoveImagesOfPhoto(B);

        Assert.DoesNotContain(tower.Images, i => i.IsThumbnail);
    }

    [Fact]
    public void Tower_RemovingThePhotoOfTheOnlyImage_LeavesNoImages()
    {
        var tower = Tower();
        tower.AddImage(A);

        tower.RemoveImagesOfPhoto(A);

        Assert.Empty(tower.Images);
    }

    [Fact]
    public void Tower_AfterTheCoverPhotoIsRemoved_ANewPhotoDoesNotStealTheCover()
    {
        var tower = Tower();
        tower.AddImage(A);
        tower.AddImage(B);
        tower.RemoveImagesOfPhoto(A);

        tower.AddImage(C);

        Assert.Equal(B, Assert.Single(tower.Images, i => i.IsThumbnail).GalleryPhotoId);
    }

    [Fact]
    public void Tower_AfterTheOnlyPhotoIsRemoved_TheNextPhotoBecomesTheCover()
    {
        var tower = Tower();
        tower.AddImage(A);
        tower.RemoveImagesOfPhoto(A);

        tower.AddImage(B);

        Assert.True(Assert.Single(tower.Images).IsThumbnail);
    }

    [Fact]
    public void Model_RemovingTheCoverPhoto_PromotesTheLowestSequenceImage()
    {
        var model = Model();
        model.AddImage(A);
        model.AddImage(B);
        model.AddImage(C);

        model.RemoveImagesOfPhoto(A);

        Assert.Equal([B, C], model.Images.Select(i => i.GalleryPhotoId));
        Assert.Equal(B, Assert.Single(model.Images, i => i.IsThumbnail).GalleryPhotoId);
    }

    [Fact]
    public void Model_RemovingANonCoverPhoto_KeepsTheCover()
    {
        var model = Model();
        model.AddImage(A);
        model.AddImage(B);

        model.RemoveImagesOfPhoto(B);

        Assert.Equal(A, Assert.Single(model.Images).GalleryPhotoId);
        Assert.True(model.Images[0].IsThumbnail);
    }

    [Fact]
    public void Model_RemovingAPhotoWhenCoverWasUnset_DoesNotInventACover()
    {
        var model = Model();
        model.AddImage(A);
        model.AddImage(B);
        model.UnsetThumbnail(model.Images[0].Id);

        model.RemoveImagesOfPhoto(B);

        Assert.DoesNotContain(model.Images, i => i.IsThumbnail);
    }

    [Fact]
    public void Project_RemovingAPhoto_UnlinksItFromEveryTowerAndModel()
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);
        var tower = project.AddTower();
        var model = project.AddModel(tower.Id, "M");
        tower.AddImage(A);
        tower.AddImage(B);
        model.AddImage(A);
        model.AddImage(B);

        project.RemoveImagesOfPhoto(A);

        Assert.Equal(B, Assert.Single(tower.Images).GalleryPhotoId);
        Assert.True(tower.Images[0].IsThumbnail);
        Assert.Equal(B, Assert.Single(model.Images).GalleryPhotoId);
        Assert.True(model.Images[0].IsThumbnail);
    }
}
