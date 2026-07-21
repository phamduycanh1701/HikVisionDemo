using System.Xml.Serialization;

namespace NtssHikvisionFaceDemo.Utils;

public static class XmlHelper
{
    public static T Deserialize<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));

        using var reader = new StringReader(xml);

        return (T)serializer.Deserialize(reader)!;
    }

    public static string Serialize<T>(T model)
    {
        var serializer = new XmlSerializer(typeof(T));

        using var sw = new StringWriter();

        serializer.Serialize(sw, model);

        return sw.ToString();
    }
}