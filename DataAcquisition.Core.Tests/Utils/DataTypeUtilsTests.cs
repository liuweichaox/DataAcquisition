using DataAcquisition.Core.Utils;
using Xunit;

namespace DataAcquisition.Core.Tests.Utils;

public class DataTypeUtilsTests
{
    [Theory]
    [InlineData((ushort)1)]
    [InlineData((uint)1)]
    [InlineData((ulong)1)]
    [InlineData((short)1)]
    [InlineData(1)]
    [InlineData((long)1)]
    [InlineData(1f)]
    [InlineData(1d)]
    public void IsNumberType_ReturnsTrueForNumericTypes(object value)
    {
        Assert.True(DataTypeUtils.IsNumberType(value));
    }

    [Theory]
    [InlineData("string")]
    [InlineData(true)]
    [InlineData('c')]
    public void IsNumberType_ReturnsFalseForNonNumericTypes(object value)
    {
        Assert.False(DataTypeUtils.IsNumberType(value));
    }
}
