// Calculates a vertical source crop whose measured horizon is exactly on its middle row.
namespace JKBar.Core.Layout;

public static class HorizonCrop
{
    public static (int Top, int Height) Centered(int imageHeight, int horizonY)
    {
        if (imageHeight <= 0 || horizonY <= 0 || horizonY >= imageHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(horizonY));
        }

        var halfHeight = Math.Min(horizonY, imageHeight - horizonY);
        return (horizonY - halfHeight, halfHeight * 2);
    }
}