namespace Reaparr.PublicAPI.UnitTests;

public class AddTorrentValidationUnitTests
{
    [Test]
    public void TorrentMetadataDTOValidator_ShouldValidateAllRequiredFields()
    {
        // Arrange
        var validator = new TorrentMetadataDTOValidator();
        var validMetadata = CreateValidTorrentMetadata();

        // Act
        var result = validator.Validate(validMetadata);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public void TorrentMetadataDTOValidator_ShouldFailValidation_WhenFieldsAreInvalid()
    {
        // Arrange
        var validator = new TorrentMetadataDTOValidator();
        var invalidMetadata = new TorrentMetadataDTO
        {
            Type = (PlexMediaType)999, // Invalid enum value
            MediaId = 0,
            DataId = 0,
            PartId = 0,
            PlexApiPartId = 0,
            VideoQuality = (VideoQuality)999, // Invalid enum value
            LibraryId = 0,
            ServerId = 0,
        };

        // Act
        var result = validator.Validate(invalidMetadata);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThan(0);

        // Check specific validation errors
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.LibraryId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.ServerId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.MediaId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.DataId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.PartId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.PlexApiPartId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.Type));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.VideoQuality));
    }

    [Test]
    public void AddTorrentEndpointRequestValidator_ShouldRequireTorrentFile()
    {
        // Arrange
        var validator = new AddTorrentEndpointRequestValidator();
        var request = new AddTorrentEndpointRequest { TorrentFile = null };

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(AddTorrentEndpointRequest.TorrentFile));
    }

    [Test]
    public void TorrentMetadataDTOValidator_ShouldRejectUnsupportedMediaTypes()
    {
        // Arrange
        var validator = new TorrentMetadataDTOValidator();
        var request = CreateValidTorrentMetadata(PlexMediaType.Season);

        // Act
        var result = validator.Validate(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(TorrentMetadataDTO.Type));
    }

    private static TorrentMetadataDTO CreateValidTorrentMetadata(PlexMediaType type = PlexMediaType.Movie) =>
        new()
        {
            Type = type,
            MediaId = 1,
            DataId = 1,
            PartId = 1,
            PlexApiPartId = 12345,
            VideoQuality = VideoQuality.HD,
            LibraryId = 1,
            ServerId = 1,
        };
}
