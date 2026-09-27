using Mya.Application.Abstractions.Media;
using Mya.Application.Features.Videos;
using Mya.Domain.Entities;
using Mya.Domain.Enums;
using Shouldly;
namespace Mya.Application.UnitTests;
public sealed class VideoRulesTests
{
    [Fact] public void Filters_combine_dimensions_with_AND_and_tags_with_OR()
    {
        var tag = Guid.NewGuid(); var other = Guid.NewGuid();
        var both = new Video { Audience = VideoAudience.Both, BodyArea = VideoBodyArea.LowerBody, RequiresEquipment = false, VideoTags = [new VideoTag { TagId = tag }] };
        var male = new Video { Audience = VideoAudience.Male, BodyArea = VideoBodyArea.LowerBody, RequiresEquipment = false, VideoTags = [new VideoTag { TagId = other }] };
        var female = new Video { Audience = VideoAudience.Female, BodyArea = VideoBodyArea.LowerBody, RequiresEquipment = true, VideoTags = [new VideoTag { TagId = other }] };
        VideoRules.Filter(new[] {both, male, female}.AsQueryable(), new() { Audience = VideoAudience.Male, BodyArea = VideoBodyArea.LowerBody, Equipment = false, Tags = [tag, other] }).ToArray().ShouldBe([both, male]);
        VideoRules.Filter(new[] {both, female}.AsQueryable(), new()).Count().ShouldBe(2);
    }

    [Fact] public void Required_classification_cannot_be_omitted()
    {
        var validator = new VideoInputValidator();
        validator.Validate(new VideoInput("Workout", null, null, null, null, [], null)).IsValid.ShouldBeFalse();
        validator.Validate(new VideoInput("Workout", null, VideoAudience.Both, VideoBodyArea.FullBody, false, [], null)).IsValid.ShouldBeTrue();
    }

    [Fact] public void Create_requires_a_described_file_alongside_the_metadata()
    {
        var validator = new VideoCreateInputValidator();
        var metadata = new VideoInput("Workout", null, VideoAudience.Both, VideoBodyArea.FullBody, false, [], null);
        validator.Validate(new VideoCreateInput(metadata, new UploadRequest(null, 10))).IsValid.ShouldBeFalse();
        validator.Validate(new VideoCreateInput(metadata, new UploadRequest("video/mp4", 0))).IsValid.ShouldBeFalse();
        validator.Validate(new VideoCreateInput(metadata, new UploadRequest("video/mp4", -1))).IsValid.ShouldBeFalse();
        validator.Validate(new VideoCreateInput(metadata, new UploadRequest("video/mp4", 10))).IsValid.ShouldBeTrue();
    }

    [Fact] public void A_reported_duration_has_to_be_plausible()
    {
        var validator = new CompleteUploadInputValidator();
        validator.Validate(new CompleteUploadInput(false, -1)).IsValid.ShouldBeFalse();
        validator.Validate(new CompleteUploadInput(false, 86_401)).IsValid.ShouldBeFalse();
        validator.Validate(new CompleteUploadInput(true, null)).IsValid.ShouldBeTrue();
        validator.Validate(new CompleteUploadInput(true, 600)).IsValid.ShouldBeTrue();
    }

    [Fact] public void Object_keys_are_derived_from_the_id_so_a_retry_cannot_orphan_bytes()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        VideoRules.ObjectKey(id, "video/mp4").ShouldBe("videos/11111111-2222-3333-4444-555555555555.mp4");
        VideoRules.ObjectKey(id, "video/quicktime").ShouldBe("videos/11111111-2222-3333-4444-555555555555.mov");
        VideoRules.ThumbnailKey(id).ShouldBe("videos/11111111-2222-3333-4444-555555555555-poster.jpg");

        // Keys must fit Video.ExternalId (64) and Video.ThumbnailObjectKey (200).
        VideoRules.ObjectKey(id, "video/quicktime").Length.ShouldBeLessThanOrEqualTo(64);
        VideoRules.ThumbnailKey(id).Length.ShouldBeLessThanOrEqualTo(200);
    }

    [Theory]
    [InlineData(1, 16, 1)]
    [InlineData(16, 16, 1)]
    [InlineData(17, 16, 2)]
    [InlineData(32, 16, 2)]
    [InlineData(33, 16, 3)]
    public void Part_arithmetic_matches_what_the_browser_will_slice(long size, long partSize, int expected) =>
        VideoStorageMath.PartCount(size, partSize).ShouldBe(expected);

    [Fact] public void A_two_gibibyte_file_at_the_default_part_size_stays_inside_the_ten_thousand_part_limit() =>
        VideoStorageMath.PartCount(2L * 1024 * 1024 * 1024, 16L * 1024 * 1024).ShouldBeLessThanOrEqualTo(10_000);

    [Fact] public void Greek_tag_spellings_collapse_to_one_normalized_name()
    {
        VideoRules.NormalizeTag(" Κοιλιακοί ").ShouldBe(VideoRules.NormalizeTag("κοιλιακοι"));
        VideoRules.NormalizeTag("Αλτήρες").ShouldNotBe(VideoRules.NormalizeTag("Λάστιχα"));
    }
}
