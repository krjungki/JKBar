// Regression checks for idle-pet choices, movement and interruption rules.
using System.Text.Json;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class PixelPetTests
{
    [Theory]
    [InlineData(IdleNotchContent.Dog)]
    [InlineData(IdleNotchContent.Cat)]
    [InlineData(IdleNotchContent.Panda)]
    public void PetSelectionSurvivesSettingsRoundTrip(IdleNotchContent content)
    {
        var settings = new JkBarSettings { Notch = new NotchSettings { IdleContent = content } };
        var restored = JsonSerializer.Deserialize<JkBarSettings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal(content, restored.Normalized().Notch.IdleContent);
        Assert.True(PixelPetAnimation.IsPet(content));
    }

    [Fact]
    public void ExistingSettingsKeepTheirMeaning()
    {
        Assert.Equal(0, (int)IdleNotchContent.Empty);
        Assert.Equal(1, (int)IdleNotchContent.DateTime);
        Assert.False(PixelPetAnimation.IsPet(IdleNotchContent.DateTime));
    }

    [Theory]
    [InlineData(IdleNotchContent.Dog)]
    [InlineData(IdleNotchContent.Cat)]
    [InlineData(IdleNotchContent.Panda)]
    public void PixelArtFitsTheFrame(IdleNotchContent content)
    {
        var sprite = PixelPetSprites.For(content);
        Assert.NotEmpty(sprite);
        Assert.True(sprite.Count < PixelPetSprites.Height - 2);
        Assert.All(sprite, row =>
        {
            Assert.Equal(PixelPetSprites.Width, row.Length);
            Assert.All(row, pixel => Assert.Contains(pixel, ".fbpen"));
        });
    }

    [Fact]
    public void MovementStaysInsideTheNotchAndIncludesRestingAndTurning()
    {
        var poses = Enumerable.Range(0, 320).Select(frame => PixelPetAnimation.At(TimeSpan.FromMilliseconds(frame * 125))).ToArray();
        Assert.All(poses, pose => { Assert.InRange(pose.Position, 0, 1); Assert.InRange(pose.Step, 0, 3); });
        Assert.Contains(poses, pose => pose.FacingLeft);
        Assert.Contains(poses, pose => !pose.FacingLeft);
        Assert.Contains(poses, pose => pose.Resting && pose.Blink);
        Assert.Equal(0.5, PixelPetAnimation.At(TimeSpan.Zero).Position);
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, false, false, true)]
    public void HiddenOrHigherPriorityContentStopsAnimation(bool visible, bool suppressed, bool alert, bool media, bool transitioning)
    {
        Assert.False(PixelPetAnimation.ShouldAnimate(IdleNotchContent.Cat, visible, suppressed, alert, media, transitioning));
        Assert.True(PixelPetAnimation.ShouldAnimate(IdleNotchContent.Cat, true, false, false, false, false));
    }

    [Fact]
    public void RoutineIncludesEveryActionAndBothWalls()
    {
        var poses = Enumerable.Range(0, 864).Select(frame => PixelPetAnimation.At(TimeSpan.FromSeconds(frame / 8d))).ToArray();
        foreach (var action in Enum.GetValues<PixelPetAction>()) Assert.Contains(poses, pose => pose.Action == action);
        Assert.Contains(poses, pose => pose.Action == PixelPetAction.Climb && pose.Position == 0 && pose.Elevation > 0.9);
        Assert.Contains(poses, pose => pose.Action == PixelPetAction.Climb && pose.Position == 1 && pose.Elevation > 0.9);
        Assert.All(poses, pose => Assert.InRange(pose.Elevation, 0, 1));
        Assert.NotEqual(PixelPetAnimation.At(TimeSpan.FromSeconds(1)).Action, PixelPetAnimation.At(TimeSpan.FromSeconds(37)).Action);
    }

    [Theory]
    [InlineData(0.75)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8.5)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(36)]
    public void ActionChangesDoNotTeleportThePet(double boundary)
    {
        var before = PixelPetAnimation.At(TimeSpan.FromSeconds(boundary - 0.001));
        var after = PixelPetAnimation.At(TimeSpan.FromSeconds(boundary + 0.001));
        Assert.InRange(Math.Abs(before.Position - after.Position), 0, 0.001);
        Assert.InRange(Math.Abs(before.Elevation - after.Elevation), 0, 0.01);
    }

    [Theory]
    [InlineData(IdleNotchContent.Dog)]
    [InlineData(IdleNotchContent.Cat)]
    [InlineData(IdleNotchContent.Panda)]
    public void EachPetHasThreeShortMessages(IdleNotchContent pet)
    {
        var messages = Enumerable.Range(0, 3).Select(variant => PixelPetAnimation.Message(pet, variant)).ToArray();
        Assert.Equal(3, messages.Distinct().Count());
        Assert.All(messages, message => Assert.InRange(message.Length, 1, 6));
    }
}