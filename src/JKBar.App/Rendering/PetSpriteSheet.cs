// Holds hand-drawn pet frames that ship as an image so the artwork is not re-encoded as source characters.
using System.Collections.Frozen;
using System.Drawing.Imaging;
using System.Reflection;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.App.Rendering;

internal readonly record struct PetFrame(Bitmap? Art, Bitmap? Rim);

internal static class PetSpriteSheet
{
    internal const int Side = 32;
    internal const int RimSide = Side + 2;
    private const int Columns = 6;

    // A panda is drawn almost entirely in black, which is also the colour of the cutout it stands in.
    private static readonly IdleNotchContent[] RimmedPets = [IdleNotchContent.Panda];

    // The sheet is laid out in the order the frame template was printed in, so the two stay in step.
    private static readonly (PixelPetAction Action, int Step, bool Blink)[] Layout =
    [
        (PixelPetAction.Climb, 0, false), (PixelPetAction.Climb, 1, false), (PixelPetAction.Climb, 2, false),
        (PixelPetAction.Climb, 3, false), (PixelPetAction.Crouch, 0, false), (PixelPetAction.Crouch, 1, false),
        (PixelPetAction.Jump, 0, false), (PixelPetAction.Jump, 1, false), (PixelPetAction.Jump, 2, false),
        (PixelPetAction.Jump, 3, false), (PixelPetAction.Land, 0, false), (PixelPetAction.Land, 1, false),
        (PixelPetAction.Land, 2, false), (PixelPetAction.Rest, 0, false), (PixelPetAction.Rest, 1, false),
        (PixelPetAction.Rest, 1, true), (PixelPetAction.Rest, 2, false), (PixelPetAction.Rest, 3, false),
        (PixelPetAction.Run, 0, false), (PixelPetAction.Run, 1, false), (PixelPetAction.Run, 2, false),
        (PixelPetAction.Run, 3, false), (PixelPetAction.Speak, 0, false), (PixelPetAction.Speak, 0, true),
        (PixelPetAction.Speak, 1, false), (PixelPetAction.Speak, 2, false), (PixelPetAction.Speak, 3, false),
        (PixelPetAction.Speak, 3, true), (PixelPetAction.Stretch, 0, false), (PixelPetAction.Stretch, 1, false),
        (PixelPetAction.Stretch, 2, false), (PixelPetAction.Stretch, 3, false), (PixelPetAction.Walk, 0, false),
        (PixelPetAction.Walk, 1, false), (PixelPetAction.Walk, 2, false), (PixelPetAction.Walk, 3, false)
    ];

    private static readonly FrozenDictionary<IdleNotchContent, FrozenDictionary<(PixelPetAction, int, bool), PetPair>> Sheets = Load();

    private readonly record struct PetPair(PetFrame Forward, PetFrame Mirrored);

    // Pets without hand-drawn art keep the generated frames, so sheets can be added one at a time.
    internal static bool Has(IdleNotchContent pet) => Sheets.ContainsKey(pet);

    internal static PetFrame Frame(IdleNotchContent pet, PixelPetPose pose)
    {
        if (!Sheets.TryGetValue(pet, out var frames)) return default;
        var step = ((pose.Step % 4) + 4) % 4;
        if (frames.TryGetValue((pose.Action, step, pose.Blink), out var drawn)
            || frames.TryGetValue((pose.Action, step, false), out drawn)
            || frames.TryGetValue((pose.Action, 0, false), out drawn))
        {
            return pose.FacingLeft ? drawn.Mirrored : drawn.Forward;
        }

        return default;
    }

    private static FrozenDictionary<IdleNotchContent, FrozenDictionary<(PixelPetAction, int, bool), PetPair>> Load()
    {
        var sheets = new Dictionary<IdleNotchContent, FrozenDictionary<(PixelPetAction, int, bool), PetPair>>();
        foreach (var (pet, resource) in new[]
                 {
                     (IdleNotchContent.Dog, "JKBar.App.Assets.pet-dog.png"),
                     (IdleNotchContent.Cat, "JKBar.App.Assets.pet-cat.png"),
                     (IdleNotchContent.Panda, "JKBar.App.Assets.pet-panda.png"),
                     (IdleNotchContent.Duck, "JKBar.App.Assets.pet-duck.png"),
                     (IdleNotchContent.Hamster, "JKBar.App.Assets.pet-hamster.png")
                 })
        {
            var frames = Slice(resource, RimmedPets.Contains(pet));
            if (frames is not null) sheets[pet] = frames;
        }

        return sheets.ToFrozenDictionary();
    }

    private static FrozenDictionary<(PixelPetAction, int, bool), PetPair>? Slice(string resource, bool rimmed)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        if (stream is null) return null;

        using var sheet = new Bitmap(stream);
        if (sheet.Width < Columns * Side || sheet.Height < Layout.Length / Columns * Side) return null;

        var frames = new Dictionary<(PixelPetAction, int, bool), PetPair>();
        for (var index = 0; index < Layout.Length; index++)
        {
            var source = new Rectangle(index % Columns * Side, index / Columns * Side, Side, Side);
            var forward = new Bitmap(Side, Side, PixelFormat.Format32bppArgb);
            using (var canvas = Graphics.FromImage(forward))
            {
                canvas.DrawImage(sheet, new Rectangle(0, 0, Side, Side), source, GraphicsUnit.Pixel);
            }

            // Mirroring once at load keeps the paint path free of per-frame image work.
            var mirrored = (Bitmap)forward.Clone();
            mirrored.RotateFlip(RotateFlipType.RotateNoneFlipX);
            frames[Layout[index]] = new PetPair(
                new PetFrame(forward, rimmed ? Rim(forward) : null),
                new PetFrame(mirrored, rimmed ? Rim(mirrored) : null));
        }

        return frames.ToFrozenDictionary();
    }

    // One pixel wider on every side, so a pet whose art already reaches the frame edge still gets a full outline.
    private static Bitmap Rim(Bitmap art)
    {
        var rim = new Bitmap(RimSide, RimSide, PixelFormat.Format32bppArgb);
        for (var y = 0; y < RimSide; y++)
        for (var x = 0; x < RimSide; x++)
        {
            if (Opaque(x - 1, y - 1)) continue;
            var touches = false;
            for (var dy = -1; dy <= 1 && !touches; dy++)
            for (var dx = -1; dx <= 1 && !touches; dx++)
                touches = Opaque(x - 1 + dx, y - 1 + dy);

            if (touches) rim.SetPixel(x, y, Color.White);
        }

        return rim;

        bool Opaque(int x, int y) => x >= 0 && y >= 0 && x < Side && y < Side && art.GetPixel(x, y).A > 8;
    }
}
