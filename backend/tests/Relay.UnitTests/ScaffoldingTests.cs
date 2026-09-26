namespace Relay.UnitTests;

/// <summary>
/// Proves the test runner is wired up before any feature code exists, so a later red build is
/// never ambiguous between the code and the scaffolding (techspec § Development Sequencing, step 1).
/// Task 3.0 replaces this with the BaselineCalculator suite.
/// </summary>
public class ScaffoldingTests
{
    [Fact]
    public void TestRunner_IsWiredUp()
    {
        Assert.True(true);
    }
}
