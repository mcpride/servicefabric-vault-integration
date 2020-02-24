using System.IO;

namespace VaultService.S3.Responses.Serializers
{
    public class NullSerializer : IS3Serializer
    {
        public void Serialize<TModel>(TModel model, Stream stream)
        {
        }

        public string Serialize<TModel>(TModel model)
        {
            return string.Empty;
        }
    }
}