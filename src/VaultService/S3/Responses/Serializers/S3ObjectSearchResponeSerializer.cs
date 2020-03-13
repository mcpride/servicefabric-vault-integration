using System.Xml;

namespace VaultService.S3.Responses.Serializers
{
    public class S3ObjectSearchSerializer : AbstractS3Serializer<S3ObjectSearchResponse>
    {
        protected override string SerializeInternal(S3ObjectSearchResponse searchResponse)
        {
            dynamic builder = new DynamicXmlBuilder();
            builder.Declaration();
            builder.ListBucketResult(new {xmlns = "http://s3.amazonaws.com/doc/2006-03-01/"},
                DynamicXmlBuilder.Fragment(list =>
                {
                    list.Name(searchResponse.BucketName);
                    list.Prefix(searchResponse.Prefix);
                    list.Marker(searchResponse.Marker);
                    if (searchResponse.MaxKeys.HasValue)
                    {
                        list.MaxKeys(searchResponse.MaxKeys.Value);
                    }
                    //list.MaxKeys(searchResponse.MaxKeys.HasValue ? searchResponse.MaxKeys.Value : int.MaxValue);
                    list.IsTruncated(XmlConvert.ToString(searchResponse.IsTruncated));
                    foreach (var s3Object in searchResponse.S3Objects)
                    {
                        var o = s3Object;
                        list.Contents(DynamicXmlBuilder.Fragment(contents =>
                        {
                            contents.Key(o.Key);
                            contents.LastModified(o.CreationDate.ToUTC());
                            contents.ETag($"\"{o.ContentMD5}\"");
                            contents.Size(o.Size);
                            contents.StorageClass("STANDARD");
                            contents.Owner(DynamicXmlBuilder.Fragment(owner =>
                            {
                                owner.ID("id");
                                owner.DisplayName("name");
                            }));
                        }));
                    }

                    foreach (var prefix in searchResponse.Prefixes)
                    {
                        var prefix1 = prefix;
                        list.CommonPrefixes(DynamicXmlBuilder.Fragment(cp => cp.Prefix(prefix1)));
                    }
                }));

            var responseBody = builder.ToString(false);
            return responseBody;
        }
    }
}