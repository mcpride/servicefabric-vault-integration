using System.Collections.Generic;
using VaultService.S3.Model;

namespace VaultService.S3.Responses
{
    public class S3ObjectSearchResponse
    {
        public S3ObjectSearchResponse()
        {
            S3Objects = new List<S3Object>();
            Prefixes = new List<string>();
        }

        public string BucketName { get; set; }
        public string Delimiter { get; set; }
        public string Marker { get; set; }
        public int? MaxKeys { get; set; }
        public string Prefix { get; set; }
        public bool IsTruncated { get; set; }
        public IList<S3Object> S3Objects { get; set; }
        public IList<string> Prefixes { get; set; }
    }
}