using System;
using VaultService.Core.Extensions;

namespace VaultService.S3.Model
{
    public class S3Object
    {
        private string _bucket;
        private string _key;
        private byte[] _content;

        public string Id { get; set; }
        // ReSharper disable once InconsistentNaming
        public string ContentMD5 { get; set; }
        public string ContentType { get; set; }
        public DateTime CreationDate { get; set; }
        public long Size { get; set; }

        public byte[] Content
        {
            get => _content;
            set
            {
                _content = value;
                Size = _content.Length;
                ContentMD5 = _content.Length == 0 ? null : _content.GenerateMD5CheckSum();
            }
        }

        public string Key
        {
            get => _key;
            set
            {
                _key = value;
                Id = _bucket + "/" + _key;
            }
        }

        public string Bucket
        {
            get => _bucket;
            set
            {
                _bucket = value;
                Id = _bucket + "/" + _key;
            }
        }
    }
}
