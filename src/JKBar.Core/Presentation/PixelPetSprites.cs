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
        var seated = action is PixelPetAction.Rest or PixelPetAction.Speak;
        var crouched = action is PixelPetAction.Crouch or PixelPetAction.Land;
        var stretched = action == PixelPetAction.Stretch;
        var flying = action == PixelPetAction.Jump;
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

        void Eye(int left, int top)
        {
            if (blink)
            {
                Fill(left, top + 2, 4, 1, 'n');
                return;
            }
            Fill(left, top, 4, 5, 'n');
            Fill(left, top, 2, 2, 'h');
            Fill(left + 2, top + 3, 1, 1, 'h');
        }

        void Head(int left, int top, bool front)
        {
            var width = front ? 22 : 16;
            if (cat)
            {
                for (var row = 0; row < 7; row++)
                {
                    Fill(left + 1, top - 3 + row, Math.Min(7, row + 2), 1, 'o');
                    Fill(left + width - 3 - Math.Min(5, row), top - 3 + row, Math.Min(7, row + 2), 1, 'o');
                }
                Fill(left + 2, top, 3, 4, 'p');
                Fill(left + width - 5, top, 3, 4, 'p');
            }
            else
            {
                var earTop = panda ? top - 2 : top + 4;
                Shape(left - 1, earTop, 8, panda ? 8 : 15, 'b');
                Shape(left + width - 7, earTop, 8, panda ? 8 : 15, 'b');
                Oval(left + 1, earTop + 2, 4, 4, panda ? 's' : 'r');
                Oval(left + width - 5, earTop + 2, 4, 4, panda ? 's' : 'r');
            }
            Shape(left, top + 1, width, front ? 18 : 15, 'f');
            Oval(left + 2, top + 11, width - 4, front ? 7 : 4, 'h');
            if (pet == IdleNotchContent.Dog)
            {
                Oval(left + 1, top + 3, front ? 8 : 6, 10, 'b');
                Fill(left + width / 2 - 1, top + 2, 3, 10, 'h');
                Shape(left - 1, top + 5, 5, 13, 'b');
                if (front) Shape(left + width - 4, top + 5, 5, 13, 'b');
            }
            if (cat)
            {
                Fill(left + width / 2 - 2, top + 2, 4, 2, 'b');
                Fill(left + width / 2 - 1, top + 4, 2, 2, 'b');
                Fill(left + 1, top + 9, 3, 2, 'b');
                Fill(left + width - 4, top + 9, 3, 2, 'b');
            }
            if (panda)
            {
                Oval(left + 3, top + 6, 7, 8, 'b');
                if (front) Oval(left + 13, top + 6, 7, 8, 'b');
            }
            Eye(left + (front ? 4 : 7), top + 7);
            if (front) Eye(left + 14, top + 7);
            Fill(left + (front ? 10 : 13), top + 12, 3, 2, 'n');
            Fill(left + (front ? 11 : 13), top + 14, 1, 1, 'n');
            if (action == PixelPetAction.Speak || cat)
                Fill(left + (front ? 10 : 12), top + 15, 3, 1, 'p');
        }

        if (seated)
        {
            if (!panda)
            {
                Shape(2 + step % 2, 20, 7, 10, 'b');
                Shape(2 + step % 2, 18, 4, 6, 'h');
            }
            Shape(8, 19, 18, 13, panda ? 'b' : 'f');
            Oval(11, 21, 12, 10, 'h');
            Shape(7, 26, 7, 6, panda ? 'b' : 's');
            Shape(21, 26, 7, 6, panda ? 'b' : 's');
            Fill(12, 26, 1, 5, 'o');
            Fill(20, 26, 1, 5, 'o');
            if (panda)
            {
                Fill(9, 28, 3, 2, 'c');
                Fill(23, 28, 3, 2, 'c');
            }
            Head(6, 4, true);
            if (!cat && !panda)
            {
                Fill(11, 22, 12, 2, 'r');
                Fill(16, 23, 3, 3, 'o');
                Fill(16, 23, 2, 2, 'c');
            }
        }
        else
        {
            var extended = flying && step is 1 or 2;
            var bodyTop = crouched ? 23 : stretched ? 22 : extended ? 18 : 19 + bob;
            if (!panda)
            {
                Shape(1, bodyTop - 5 - step % 2, 4, 9, 'b');
                Fill(2, bodyTop - 4 - step % 2, 2, 3, 'h');
                Shape(3, bodyTop, 6, 4, 'b');
            }
            else Shape(4, bodyTop + 1, 5, 4, 'h');
            Shape(extended ? 4 : 6, bodyTop, extended ? 24 : 20, crouched || extended ? 6 : 9, panda ? 'b' : 'f');
            Oval(10, bodyTop + 3, 12, 5, 'h');
            if (cat) Fill(10, bodyTop + 1, 3, 3, 'b');
            if (pet == IdleNotchContent.Dog) Oval(8, bodyTop + 1, 7, 4, 'b');
            var reach = action == PixelPetAction.Run ? 3 : 1;
            var stride = crouched ? 0 : flying ? (extended ? 3 : -1) : step switch { 0 => -reach, 1 => 0, 2 => reach, _ => 1 };
            var footTop = flying ? bodyTop + 5 : 27;
            Shape(8 - stride, footTop, 5, flying ? 4 : 5, panda ? 'b' : 'h');
            Shape(21 + stride, footTop, 5, flying ? 4 : 5, panda ? 'b' : 'h');
            if (stretched) Shape(24, 28, 7, 4, 'h');
            Head(15, crouched ? 12 : stretched ? 14 : extended ? 9 : 7 + bob, false);
            if (pet == IdleNotchContent.Dog)
            {
                Fill(18, bodyTop + 1, 8, 2, 'r');
                Fill(24, bodyTop + 3, 2, 2, 'c');
            }
        }
        return Array.AsReadOnly(pixels.Select(row => new string(row)).ToArray());
    }
}