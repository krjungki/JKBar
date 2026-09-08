// Deterministic idle-pet movement, independent of rendering and wall-clock time.
using JKBar.Core.Settings;

namespace JKBar.Core.Presentation;

public enum PixelPetAction { Walk, Jump, Climb, Speak, Rest }

public readonly record struct PixelPetPose(
    double Position, bool FacingLeft, int Step, bool Resting, bool Blink,
    PixelPetAction Action = PixelPetAction.Walk, double Elevation = 0, int Message = 0);

public static class PixelPetAnimation
{
    public static bool IsPet(IdleNotchContent content) =>
        content is IdleNotchContent.Dog or IdleNotchContent.Cat or IdleNotchContent.Panda;

    public static bool ShouldAnimate(IdleNotchContent content, bool visible, bool suppressed, bool alert, bool media, bool transitioning) =>
        IsPet(content) && visible && !suppressed && !alert && !media && !transitioning;

    public static PixelPetPose At(TimeSpan elapsed)
    {
        var seconds = Math.Max(0, elapsed.TotalSeconds);
        var phase = seconds % 36;
        var leftWall = phase >= 18;
        var leg = phase % 18;
        var position = leg < 4 ? 0.5 + leg / 8 : leg < 8.5 ? 1 : leg < 12 ? 1 - (leg - 8.5) / 7 : 0.5;
        var action = leg switch
        {
            < 4 => PixelPetAction.Walk,
            < 7 => PixelPetAction.Climb,
            < 8.5 => PixelPetAction.Jump,
            < 12 => PixelPetAction.Walk,
            < 15 => PixelPetAction.Speak,
            _ => PixelPetAction.Rest
        };
        var elevation = action switch
        {
            PixelPetAction.Climb => Math.Sin((leg - 4) / 3 * Math.PI),
            PixelPetAction.Jump => Arc((leg - 7) / 1.5),
            _ => 0
        };
        var hopStart = (int)(seconds / 36 % 2) == 0 ? 0.75 : 1.75;
        if (leg >= hopStart && leg < hopStart + 1.25)
        {
            action = PixelPetAction.Jump;
            elevation = Arc((leg - hopStart) / 1.25);
        }
        var resting = action is PixelPetAction.Rest or PixelPetAction.Speak;
        return new PixelPetPose(
            leftWall ? 1 - position : position,
            leg >= 8.5 ? !leftWall : leftWall,
            resting ? 0 : (int)(seconds * 8 % 4),
            resting,
            resting && leg % 1.5 < 0.25,
            action,
            Math.Clamp(elevation, 0, 1),
            (int)(seconds / 18 % 3));
    }

    public static string Message(IdleNotchContent pet, int variant) => (pet, variant % 3) switch
    {
        (IdleNotchContent.Dog, 0) => "멍!",
        (IdleNotchContent.Dog, 1) => "같이 놀자!",
        (IdleNotchContent.Dog, _) => "간식 시간?",
        (IdleNotchContent.Cat, 0) => "야옹~",
        (IdleNotchContent.Cat, 1) => "뭐 하냥?",
        (IdleNotchContent.Cat, _) => "잠깐 쉬자",
        (IdleNotchContent.Panda, 0) => "안녕!",
        (IdleNotchContent.Panda, 1) => "대나무!",
        (IdleNotchContent.Panda, _) => "데굴데굴~",
        _ => string.Empty
    };

    private static double Arc(double progress) => 4 * progress * (1 - progress);
}