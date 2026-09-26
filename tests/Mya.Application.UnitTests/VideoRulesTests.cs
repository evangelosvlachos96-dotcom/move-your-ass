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
}
