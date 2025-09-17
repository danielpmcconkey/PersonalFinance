using Lib.DataTypes;
using Lib.Utils;

namespace Lib.Tests.Utils;

public class MathFuncTests
{
    [Fact]
    public void FlipACoin_ShouldReturnApproximately50PercentHeadsAndTails()
    {
        // Arrange
        const int totalFlips = 10000;
        const double expectedPercentage = 0.5;
        const double tolerance = 0.05; // 5% tolerance
        int headsCount = 0;
        int tailsCount = 0;

        // Act
        for (int i = 0; i < totalFlips; i++)
        {
            var result = MathFunc.FlipACoin();
            if (result == CoinFlip.Heads)
                headsCount++;
            else if (result == CoinFlip.Tails)
                tailsCount++;
        }

        // Assert
        double headsPercentage = (double)headsCount / totalFlips;
        double tailsPercentage = (double)tailsCount / totalFlips;

        Assert.True(Math.Abs(headsPercentage - expectedPercentage) < tolerance, 
            $"Heads percentage {headsPercentage:P2} should be within {tolerance:P0} of {expectedPercentage:P0}");
        
        Assert.True(Math.Abs(tailsPercentage - expectedPercentage) < tolerance,
            $"Tails percentage {tailsPercentage:P2} should be within {tolerance:P0} of {expectedPercentage:P0}");
        
        Assert.Equal(totalFlips, headsCount + tailsCount);
    }

}