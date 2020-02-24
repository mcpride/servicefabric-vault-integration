using System.IO;

namespace VaultService.S3.Responses.Serializers
{
    public abstract class AbstractS3Serializer<TModel> : IS3Serializer
    {
        void IS3Serializer.Serialize<T>(T t, Stream stream)
        {
            var model = (TModel) (object) t;
            var response = SerializeInternal(model);
            var sw = new StreamWriter(stream);
            sw.Write(response);
            sw.Flush();
        }

        string IS3Serializer.Serialize<T>(T t)
        {
            var model = (TModel)(object)t;
            return SerializeInternal(model);
        }

        protected abstract string SerializeInternal(TModel model);
    }
}