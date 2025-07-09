namespace DataAcquisition.Core.Utils;

public static class DataTypeUtils
{
    public static bool IsNumberType(object value)
    {
        return value is ushort or uint or ulong or short or int or long or float or double;
    }
}