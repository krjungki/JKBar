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
            Shape(left, top, 6, height, colour);
            Fill(left + 1, top + height - 3, 4, 2, 'h');
        }

        /// Turned towards the viewer on a body that stays side on, which is what keeps the pet readable in motion.
        void Head(int left, int top)
        {
            const int width = 16;
            if (dog)
            {
                // Tall upright ears with a pink lining are the corgi's most recognisable feature.
                Shape(left - 1, top - 6, 7, 10, 'f');
                Shape(left + width - 6, top - 6, 7, 10, 'f');
                Fill(left + 1, top - 4, 3, 6, 'p');
                Fill(left + width - 4, top - 4, 3, 6, 'p');
            }
            else if (cat)
            {
                for (var row = 0; row < 6; row++)
                {
                    Fill(left + 1, top - 6 + row, row + 2, 1, 'o');
                    Fill(left + width - 3 - row, top - 6 + row, row + 2, 1, 'o');
                }

                Fill(left + 2, top - 3, 2, 3, 'p');
                Fill(left + width - 4, top - 3, 2, 3, 'p');
            }
            else
            {
                Shape(left, top - 6, 8, 8, 'b');
                Shape(left + width - 8, top - 6, 8, 8, 'b');
            }

            Shape(left, top, width, 14, coat);

            if (dog)
            {
                Fill(left + 6, top + 1, 4, 6, 'h');
            }
            else if (cat)
            {
                Fill(left + 2, top + 1, 5, 3, 'b');
                Fill(left + 10, top + 1, 4, 3, 'b');
            }
            else
            {
                Oval(left + 1, top + 2, 6, 7, 'b');
                Oval(left + 9, top + 2, 6, 7, 'b');
            }

            // The pale muzzle is drawn before the eyes so the dots sit on the boundary, as they do in real sprites.
            Oval(left + 3, top + 6, 11, 8, 'h');
            Eye(left + 3, top + 4);
            Eye(left + 10, top + 4);
            Fill(left + 7, top + 7, 3, 2, 'n');

            if (dog || action == PixelPetAction.Speak)
            {
                Fill(left + 7, top + 10, 3, 2, 'p');
            }
            else
            {
                Fill(left + 6, top + 10, 2, 1, 'n');
                Fill(left + 10, top + 10, 2, 1, 'n');
            }
        }

        // Chibi proportions: the head is nearly as deep as the body is long, which is what reads as cute.
        var legHeight = seated ? 3 : 5;
        var bodyHeight = seated ? 12 : crouched ? 8 : 11;
        var bodyWidth = seated ? 15 : 20;
        var bodyLeft = seated ? 5 : 2;
        var bodyBottom = 30 - legHeight + 2 + bob - (flying ? 3 : 0);
        var bodyTop = bodyBottom - bodyHeight;
        var legTop = bodyBottom - 2;
        var reach = running ? 4 : flying ? 5 : 3;
        var stride = seated || crouched ? 0 : step switch { 0 => -reach, 1 => 0, 2 => reach, _ => 0 };

        if (!panda)
        {
            var wag = step % 2;
            if (cat)
            {
                Shape(bodyLeft - 2, bodyTop - 9 + wag, 5, 14, 'b');
            }
            else
            {
                Shape(bodyLeft - 2, bodyTop - 1 - wag, 6, 7, 'f');
            }
        }

        // The far leg of each pair is offset by a couple of pixels rather than by the stride, so the four legs
        // read as two pairs with a gap between them instead of one continuous band.
        var frontLeg = bodyLeft + bodyWidth - 9;
        var rearLeg = bodyLeft + 1;
        Paw(rearLeg + stride + 2, legTop, legHeight, 's');
        Paw(frontLeg - stride + 2, legTop - (stretched ? 3 : 0), legHeight, 's');

        Shape(bodyLeft, bodyTop, bodyWidth, bodyHeight, panda ? 'b' : coat);
        Oval(bodyLeft + 4, bodyTop + bodyHeight - 5, bodyWidth - 11, 4, 'h');
        if (cat)
        {
            Fill(bodyLeft + 5, bodyTop + 1, 2, 5, 'b');
            Fill(bodyLeft + 10, bodyTop + 1, 2, 5, 'b');
        }

        Paw(rearLeg + stride, legTop, legHeight, coat);
        Paw(frontLeg - stride, legTop - (stretched ? 3 : 0), legHeight, coat);

        Head(bodyLeft + bodyWidth - 8, bodyTop - (seated ? 12 : 11));
        return Array.AsReadOnly(pixels.Select(row => new string(row)).ToArray());
    }
}