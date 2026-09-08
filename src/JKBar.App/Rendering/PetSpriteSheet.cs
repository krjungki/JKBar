// Holds hand-drawn pet frames that ship as an image so the artwork is not re-encoded as source characters.
using System.Collections.Frozen;
using System.Drawing.Imaging;
using System.Reflection;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.App.Rendering;

internal static class PetSpriteSheet
{
    internal const int Side = 32;
    private const int Columns = 6;

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

    private static readonly FrozenDictionary<IdleNotchContent, FrozenDictionary<(PixelPetAction, int, bool), Bitmap[]>> Sheets = Load();

    // Pets without hand-drawn art keep the generated frames, so sheets can be added one at a time.
    internal static bool Has(IdleNotchContent pet) => Sheets.ContainsKey(pet);

    internal static Bitmap? Frame(IdleNotchContent pet, PixelPetPose pose)
    {
        if (!Sheets.TryGetValue(pet, out var frames)) return null;
        var step = ((pose.Step % 4) + 4) % 4;
        if (frames.TryGetValue((pose.Action, step, pose.Blink), out var pair)
            || frames.TryGetValue((pose.Action, step, false), out pair)
            || frames.TryGetValue((pose.Action, 0, false), out pair))
        {
            return pair[pose.FacingLeft ? 1 : 0];
        }

        return null;
    }

    private static FrozenDictionary<IdleNotchContent, FrozenDictionary<(PixelPetAction, int, bool), Bitmap[]>> Load()
    {
        var sheets = new Dictionary<IdleNotchContent, FrozenDictionary<(PixelPetAction, int, bool), Bitmap[]>>();
        foreach (var (pet, resource) in new[] { (IdleNotchContent.Dog, "JKBar.App.Assets.pet-dog.png") })
        {
            var frames = Slice(resource);
            if (frames is not null) sheets[pet] = frames;
        }

        return sheets.ToFrozenDictionary();
    }

    private static FrozenDictionary<(PixelPetAction, int, bool), Bitmap[]>? Slice(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        if (stream is null) return null;

        using var sheet = new Bitmap(stream);
        if (sheet.Width < Columns * Side || sheet.Height < Layout.Length / Columns * Side) return null;

        var frames = new Dictionary<(PixelPetAction, int, bool), Bitmap[]>();
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
            frames[Layout[index]] = [forward, mirrored];
        }

        return frames.ToFrozenDictionary();
    }
}
