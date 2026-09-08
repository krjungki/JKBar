// Original pixel-art frames: the body is drawn side on and only the head turns towards the viewer.
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
        var coat = panda ? 'h' : 'f';
        var bob = !seated && !crouched && !flying && step % 2 == 1 ? 1 : 0;

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

        // Small dot eyes with one catchlight; anything larger merges into a single dark band across the face.
        void Eye(int left, int top)
        {
            if (blink)
            {
                Fill(left, top + 2, 3, 1, 'n');
                return;
            }

            Fill(left, top, 3, 4, 'n');
            Fill(left, top, 1, 1, 'h');
        }

        void Paw(int left, int top, int height, char colour)
        {
            Shape(left, top, 5, height, colour);
            Fill(left + 1, top + height - 3, 3, 2, 'h');
        }

        /// Turned towards the viewer on a body that stays side on, which is what keeps the pet readable in motion.
        void Head(int left, int top)
        {
            const int width = 13;
            if (dog)
            {
                Shape(left - 1, top - 7, 6, 10, 'f');
                Shape(left + 8, top - 7, 6, 10, 'f');
                Fill(left + 1, top - 5, 2, 6, 'p');
                Fill(left + 10, top - 5, 2, 6, 'p');
            }
            else if (cat)
            {
                for (var row = 0; row < 5; row++)
                {
                    Fill(left + 1, top - 5 + row, row + 2, 1, 'o');
                    Fill(left + width - 3 - row, top - 5 + row, row + 2, 1, 'o');
                }

                Fill(left + 2, top - 3, 2, 3, 'p');
                Fill(left + width - 4, top - 3, 2, 3, 'p');
            }
            else
            {
                Shape(left - 1, top - 5, 7, 7, 'b');
                Shape(left + 7, top - 5, 7, 7, 'b');
            }

            Shape(left, top, width, 12, coat);

            if (dog)
            {
                // The corgi's white blaze and muzzle, which is most of what makes the breed recognisable.
                Fill(left + 5, top + 1, 3, 5, 'h');
                Oval(left + 3, top + 6, 9, 6, 'h');
            }
            else if (cat)
            {
                Fill(left + 2, top + 1, 4, 2, 'b');
                Fill(left + 8, top + 1, 3, 2, 'b');
                Oval(left + 3, top + 6, 9, 6, 'h');
            }
            else
            {
                Oval(left + 1, top + 2, 5, 6, 'b');
                Oval(left + 7, top + 2, 5, 6, 'b');
                Oval(left + 3, top + 6, 9, 6, 'h');
            }

            Eye(left + 2, top + 3);
            Eye(left + 8, top + 3);
            Oval(left + 5, top + 7, 4, 3, 'n');

            if (dog || action == PixelPetAction.Speak)
            {
                Fill(left + 6, top + 10, 3, 2, 'p');
            }
            else
            {
                Fill(left + 4, top + 10, 2, 1, 'n');
                Fill(left + 8, top + 10, 2, 1, 'n');
            }
        }

        // The body is side on. Only the stride, the height off the ground and the tail change between actions.
        var legHeight = seated ? 3 : dog ? 5 : crouched ? 4 : 6;
        var bodyHeight = seated ? 13 : crouched ? 8 : 10;
        var bodyWidth = seated ? 13 : 16;
        var bodyLeft = seated ? 7 : 2;
        var bodyBottom = 31 - legHeight + 3 + bob - (flying ? 3 : 0);
        var bodyTop = bodyBottom - bodyHeight;
        var legTop = bodyBottom - 2;
        var reach = running ? 4 : flying ? 5 : 3;
        var stride = seated || crouched ? 0 : step switch { 0 => -reach, 1 => 0, 2 => reach, _ => 0 };

        if (!panda)
        {
            var wag = step % 2;
            if (cat)
            {
                Shape(bodyLeft - 2, bodyTop - 8 + wag, 5, 13, 'b');
            }
            else
            {
                Shape(bodyLeft - 2, bodyTop + 1 - wag, 5, 6, 'f');
            }
        }

        var frontLeg = bodyLeft + bodyWidth - 6;
        var rearLeg = bodyLeft + 1;
        Paw(rearLeg - stride, legTop, legHeight, 's');
        Paw(frontLeg + stride, legTop - (stretched ? 3 : 0), legHeight, 's');

        Shape(bodyLeft, bodyTop, bodyWidth, bodyHeight, panda ? 'b' : coat);
        Oval(bodyLeft + 3, bodyTop + bodyHeight - 5, bodyWidth - 7, 4, 'h');
        if (cat)
        {
            Fill(bodyLeft + 4, bodyTop + 1, 2, 4, 'b');
            Fill(bodyLeft + 8, bodyTop + 1, 2, 4, 'b');
        }

        Paw(rearLeg + stride, legTop, legHeight, coat);
        Paw(frontLeg - stride, legTop - (stretched ? 3 : 0), legHeight, coat);

        if (dog)
        {
            Fill(frontLeg - 1, bodyTop + 1, 7, 3, 'r');
            Fill(frontLeg + 1, bodyTop + 4, 3, 2, 'r');
        }

        Head(bodyLeft + bodyWidth - (seated ? 8 : 5), bodyTop - (seated ? 10 : 9));
        return Array.AsReadOnly(pixels.Select(row => new string(row)).ToArray());
    }
}