using System.Collections.Generic;
using DataAcquisition.Core.Utils;
using Xunit;

namespace DataAcquisition.Core.Tests.Utils;

public class Sha256UtilsTests
{
    [Fact]
    public void ComputeSha256HashForDictionary_ReturnsSameHashRegardlessOfKeyOrder()
    {
        var dict1 = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 };
        var dict2 = new Dictionary<string, object> { ["b"] = 2, ["a"] = 1 };

        var hash1 = Sha256Utils.ComputeSha256HashForDictionary(dict1);
        var hash2 = Sha256Utils.ComputeSha256HashForDictionary(dict2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeSha256HashForDictionary_ReturnsExpectedHash()
    {
        var dict = new Dictionary<string, object> { ["b"] = 2, ["a"] = 1 };
        var expected = "43258cff783fe7036d8a43033f830adfc60ec037382473548ac742b888292777";

        var hash = Sha256Utils.ComputeSha256HashForDictionary(dict);

        Assert.Equal(expected, hash);
    }
}
