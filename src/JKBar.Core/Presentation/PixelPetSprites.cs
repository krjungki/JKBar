// Original, cached pixel-art frames with separate seated and moving silhouettes.
using JKBar.Core.Settings;

namespace JKBar.Core.Presentation;

public static class PixelPetSprites
{
    public const int Width = 32;
    public const int Height = 32;
    public const string Palette = ".ofbshpnrc";

    private static readonly Dictionary<(IdleNotchContent Pet, PixelPetAction Action, int Step, bool Blink), IReadOnlyList<string>> Frames = CreateFrames();

    public static IReadOnlyList<string> For(IdleNotchContent content) =>
        For(content, new PixelPetPose(0.5, false, 0, true, false, PixelPetAction.Rest));

    public static IReadOnlyList<string> For(IdleNotchContent content, PixelPetPose pose) =>
        Frames.TryGetValue((content, pose.Action, Math.Clamp(pose.Step, 0, 3), pose.Blink), out var frame)
            ? frame : Array.Empty<string>();

    private static Dictionary<(IdleNotchContent, PixelPetAction, int, bool), IReadOnlyList<string>> CreateFrames()
    {
        var frames = new Dictionary<(IdleNotchContent, PixelPetAction, int, bool), IReadOnlyList<string>>();
        foreach (var pet in new[] { IdleNotchContent.Dog, IdleNotchContent.Cat, IdleNotchContent.Panda })
        foreach (var action in Enum.GetValues<PixelPetAction>())
        for (var step = 0; step < 4; step++)
        foreach (var blink in new[] { false, true })
            frames[(pet, action, step, blink)] = Build(pet, action, step, blink);
        return frames;
    }

    private static IReadOnlyList<string> Build(IdleNotchContent pet, PixelPetAction action, int step, bool blink)
    {
        var pixels = Enumerable.Range(0, Height).Select(_ => Enumerable.Repeat('.', Width).ToArray()).ToArray();
        var panda = pet == IdleNotchContent.Panda;
        var cat = pet == IdleNotchContent.Cat;
        var dog = pet == IdleNotchContent.Dog;
        var seated = action is PixelPetAction.Rest or PixelPetAction.Speak;
        var crouched = action is PixelPetAction.Crouch or PixelPetAction.Land;
        var stretched = action == PixelPetAction.Stretch;
        var flying = action == PixelPetAction.Jump;
        var running = action == PixelPetAction.Run;
        var bob = !seated && !crouched && step % 2 == 1 ? 1 : 0;

        void Fill(int left, int top, int width, int height, char colour)
        {
            for (var row = Math.Max(0, top); row < Math.Min(Height, top + height); row++)
            for (var column = Math.Max(0, left); column < Math.Min(Width, left + width); column++)
                pixels[row][column] = colour;
        }

        void Oval(int left, int top, int width, int height, char colour)
        {
            for (var row = 0; row < height; row++)
            for (var column = 0; column < width; column++)
            {
                var horizontal = (2d * column + 1 - width) / width;
                var vertical = (2d * row + 1 - height) / height;
                if (horizontal * horizontal + vertical * vertical <= 1)
                    Fill(left + column, top + row, 1, 1, colour);
            }
        }

        void Shape(int left, int top, int width, int height, char colour)
        {
            Oval(left, top, width, height, 'o');
            Oval(left + 1, top + 1, width - 2, height - 2, colour);
        }

        // Round eyes with a bright catchlight; a closed eye curves upward so a blink reads as a smile.
        void Eye(int left, int top)
        {
            if (blink)
            {
                Fill(left + 1, top + 3, 3, 1, 'n');
                Fill(left, top + 2, 1, 1, 'n');
                Fill(left + 4, top + 2, 1, 1, 'n');
                return;
            }

            Oval(left, top, 5, 6, 'n');
            Fill(left + 1, top + 1, 2, 2, 'h');
            Fill(left + 3, top + 4, 1, 1, 'h');
        }

        /// The face is always drawn front on, because a profile loses the eyes that make the pet readable.
        void Head(int left, int top)
        {
            const int width = 18;
            if (cat)
            {
                for (var row = 0; row < 5; row++)
                {
                    Fill(left + 1, top - 4 + row, row + 2, 1, 'o');
                    Fill(left + width - 3 - row, top - 4 + row, row + 2, 1, 'o');
                }

                Fill(left + 2, top - 2, 2, 2, 'p');
                Fill(left + width - 4, top - 2, 2, 2, 'p');
            }
            else if (panda)
            {
                Shape(left, top - 4, 7, 7, 'b');
                Shape(left + width - 7, top - 4, 7, 7, 'b');
            }
            else
            {
                Shape(left - 3, top + 2, 6, 13, 'b');
                Shape(left + width - 3, top + 2, 6, 13, 'b');
            }

            Shape(left, top, width, 16, panda ? 'h' : 'f');
            if (dog)
            {
                Oval(left + 1, top + 1, 7, 6, 'b');
                Oval(left + 11, top + 2, 6, 4, 'b');
            }

            if (cat)
            {
                Fill(left + 2, top + 1, 5, 2, 'b');
                Fill(left + 3, top + 4, 3, 1, 'b');
                Fill(left + 12, top + 1, 4, 2, 'b');
            }

            // The eye stays readable inside a dark marking only if a pale ring separates the two.
            if (panda)
            {
                Oval(left + 1, top + 3, 8, 9, 'b');
                Oval(left + 9, top + 3, 8, 9, 'b');
                Oval(left + 2, top + 4, 6, 7, 'h');
                Oval(left + 10, top + 4, 6, 7, 'h');
            }

            Oval(left + 4, top + 9, 10, 6, 'h');
            Eye(left + 3, top + 5);
            Eye(left + 10, top + 5);
            Oval(left, top + 9, 4, 3, 'p');
            Oval(left + width - 4, top + 9, 4, 3, 'p');

            Oval(left + 7, top + 10, 4, 3, 'n');
            if (action == PixelPetAction.Speak)
            {
                Oval(left + 7, top + 13, 4, 3, 'p');
            }
            else
            {
                Fill(left + 6, top + 13, 2, 1, 'n');
                Fill(left + 10, top + 13, 2, 1, 'n');
            }
        }

        // Everything hangs off one upright pose: only the drop, the stride and the tail change between actions.
        var drop = crouched ? 3 : stretched ? 1 : flying ? -1 : bob;
        var headTop = 4 + drop;
        var bodyTop = 19 + drop;
        var bodyHeight = crouched ? 8 : 10;
        var reach = running ? 4 : seated ? 0 : 2;
        var stride = flying ? 3 : seated || crouched ? 0 : step switch { 0 => -reach, 1 => 0, 2 => reach, _ => 0 };

        if (!panda)
        {
            var wag = seated ? step % 2 : step % 2 + 1;
            Shape(3, bodyTop - 3 - wag, 5, 10, 'b');
            Fill(4, bodyTop - 2 - wag, 2, 4, 'h');
        }

        Shape(9, bodyTop, 14, bodyHeight, panda ? 'b' : 'f');
        Oval(12, bodyTop + 2, 8, bodyHeight - 2, 'h');

        var footTop = flying ? bodyTop + 5 : bodyTop + 7;
        Shape(8 - stride, footTop - (stretched ? 2 : 0), 7, 5, panda ? 'b' : 'h');
        Shape(17 + stride, footTop, 7, 5, panda ? 'b' : 'h');

        if (panda)
        {
            Shape(6, bodyTop + 2, 5, 5, 'b');
            Shape(21, bodyTop + 2, 5, 5, 'b');
        }

        if (dog)
        {
            Fill(10, bodyTop + 1, 12, 2, 'r');
            Fill(15, bodyTop + 3, 2, 2, 'c');
        }

        Head(7, headTop);
        return Array.AsReadOnly(pixels.Select(row => new string(row)).ToArray());
    }
}