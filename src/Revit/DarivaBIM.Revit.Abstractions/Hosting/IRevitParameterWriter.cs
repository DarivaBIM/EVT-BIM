namespace DarivaBIM.Revit.Abstractions.Hosting
{
    public interface IRevitParameterWriter
    {
        bool WriteString(long elementId, string parameterName, string value);

        bool WriteInteger(long elementId, string parameterName, int value);

        bool WriteDouble(long elementId, string parameterName, double value);
    }
}
