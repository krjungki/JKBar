// Original pixel-art silhouettes; dots are transparent and letters select palette entries.
using JKBar.Core.Settings;

namespace JKBar.Core.Presentation;

public static class PixelPetSprites
{
    public const int Width = 16;
    public const int Height = 18;

    private static readonly string[] Dog =
    [
        "................",
        "................",
        ".........bb.....",
        "........bffbb...",
        "........bfffbb..",
        ".......bfffffb..",
        ".......bffeffe..",
        "...fffffffffff..",
        "..ffffffffpfffn.",
        "..ffffffffffff..",
        "..ffffffbbfff...",
        "..ffffffppbb....",
        "...ffffffff.....",
        "....ffffff......"
    ];

    private static readonly string[] Cat =
    [
        "................",
        "........b....b..",
        "........bb..bb..",
        "........bpbbpb..",
        ".......bffffffb.",
        ".......bffffffb.",
        ".......bffeffeb.",
        "...bbbffffffffb.",
        "..bffffffffpnfb.",
        "..bfffffffffff..",
        "..bfffbbfffff...",
        "..bffffffffff...",
        "...bfffffff.....",
        "....ffffff......"
    ];

    private static readonly string[] Panda =
    [
        "................",
        "........bb..bb..",
        ".......bbbbbbbb.",
        ".......bffffffb.",
        ".......ffffffff.",
        ".......ffbbffbb.",
        ".......ffbeffbe.",
        "...bbffffffffff.",
        "..bffffffffpnff.",
        "..bfffffffffff..",
        "..bbffbbfffff...",
        "..bbffbbbfff....",
        "...bfffffff.....",
        "....ffffff......"
    ];

    public static IReadOnlyList<string> For(IdleNotchContent content) => content switch
    {
        IdleNotchContent.Dog => Array.AsReadOnly(Dog),
        IdleNotchContent.Cat => Array.AsReadOnly(Cat),
        IdleNotchContent.Panda => Array.AsReadOnly(Panda),
        _ => Array.Empty<string>()
    };
}