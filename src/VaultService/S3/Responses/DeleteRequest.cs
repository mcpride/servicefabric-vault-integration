using System;
using System.Xml.Serialization;

namespace VaultService.S3.Responses
{
    [Serializable]
    [XmlRoot("Delete")]
    public class DeleteRequest
    {
        [XmlElement("Object")] public DeleteObject Object { get; set; }
    }

    [Serializable]
    public class DeleteObject
    {
        public string Key { get; set; }
    }
}