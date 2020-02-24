using System.IO;

namespace VaultService.S3.Responses.Serializers
{
    public interface IS3Serializer
    {
        void Serialize<TModel>(TModel model, Stream stream);
        string Serialize<TModel>(TModel model);
    }
}