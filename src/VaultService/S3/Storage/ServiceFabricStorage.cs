using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using VaultService.Core.Extensions;
using VaultService.S3.Model;
using VaultService.S3.Responses;

namespace VaultService.S3.Storage
{
    public class ServiceFabricStorageOptions
    {
        private int _defaultTimeoutFromSeconds;

        public int DefaultTimeoutFromSeconds
        {
            get => _defaultTimeoutFromSeconds < 1 ? 10 : _defaultTimeoutFromSeconds;
            set => _defaultTimeoutFromSeconds = value;
        }
    }

    public class ServiceFabricStorage : IS3Storage
    {
        public class S3Entry
        {
            public string Hash { get; set; }
            public string ContentType { get; set; }
            public DateTime CreationDate { get; set; }
            public long Size { get; set; }
        }

        public static readonly string S3BucketsKey = "S3:Buckets";
        public static readonly string S3EntriesKey = "S3:Entries";
        public static readonly string S3ContentsKey = "S3:Contents";
        public static readonly char PathDelimiter = '/';

        private readonly TimeSpan _defaultTimeout;
        private readonly IReliableStateManager _stateManager;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly IOptionsSnapshot<ServiceFabricStorageOptions> _options;

        public ServiceFabricStorage(IReliableStateManager stateManager, IOptionsSnapshot<ServiceFabricStorageOptions> options)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.Value == null) throw new InvalidOperationException("ServiceFabricStorageOptions value is null!");
            _defaultTimeout = TimeSpan.FromSeconds(_options.Value.DefaultTimeoutFromSeconds);
        }

        #region Buckets

        public async Task AddBucketAsync(Bucket bucket)
        {
            await AddBucketAsync(bucket, CancellationToken.None);
        }

        public async Task AddBucketAsync(Bucket bucket, CancellationToken cancellationToken)
        {
            if (bucket == null) throw new ArgumentNullException(nameof(bucket));
            var dictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
            using (var tx = _stateManager.CreateTransaction())
            {
                if (await dictionary.ContainsKeyAsync(tx, bucket.Id, _defaultTimeout, cancellationToken)) return;
                await dictionary.AddOrUpdateAsync(tx, bucket.Id, bucket, (k, v) => v, _defaultTimeout, cancellationToken);
                await tx.CommitAsync();
            }
        }

        public async Task DeleteBucketAsync(string bucketId)
        {
            await DeleteBucketAsync(bucketId, CancellationToken.None);
        }

        public async Task DeleteBucketAsync(string bucketId, CancellationToken cancellationToken)
        {
            if (bucketId == null) return;
            var dictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
            using (var tx = _stateManager.CreateTransaction())
            {
                await dictionary.TryRemoveAsync(tx, bucketId, _defaultTimeout, cancellationToken);
                await tx.CommitAsync();
            }
        }

        public async Task<Bucket> GetBucketAsync(string bucketId)
        {
            return await GetBucketAsync(bucketId, CancellationToken.None);
        }

        public async Task<Bucket> GetBucketAsync(string bucketId, CancellationToken cancellationToken)
        {
            if (bucketId == null) return null;
            var dictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
            using (var tx = _stateManager.CreateTransaction())
            {
                var result = await dictionary.TryGetValueAsync(tx, bucketId, _defaultTimeout, cancellationToken);
                return result.HasValue ? result.Value : null;
            }
        }

        public async Task<IEnumerable<Bucket>> ListBucketsAsync()
        {
            return await ListBucketsAsync(CancellationToken.None);
        }

        public async Task<IEnumerable<Bucket>> ListBucketsAsync(CancellationToken cancellationToken)
        {
            var results = new List<Bucket>();
            var dictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
            using (var tx = _stateManager.CreateTransaction())
            {
                var enumerator = (await dictionary.CreateEnumerableAsync(tx, EnumerationMode.Ordered)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(cancellationToken)) results.Add(enumerator.Current.Value);
            }
            return results;
        }

        #endregion Buckets

        #region Objects

        public async Task AddObjectAsync(S3Object s3Object)
        {
            await AddObjectAsync(s3Object, CancellationToken.None);
        }

        public async Task AddObjectAsync(S3Object s3Object, CancellationToken cancellationToken)
        {
            if (s3Object == null) throw new ArgumentNullException(nameof(s3Object));
            if (string.IsNullOrEmpty(s3Object.Bucket)) throw new InvalidOperationException("Bucket of S3 object must not be empty!");
            if (string.IsNullOrEmpty(s3Object.Key)) throw new InvalidOperationException("Key of S3 object must not be empty!");

            using (var tx = _stateManager.CreateTransaction())
            {
                //Save the content value (byte array)
                var dictContentsName = $"{S3ContentsKey}:{s3Object.Bucket}";
                var dictContents = await _stateManager.GetOrAddAsync<IReliableDictionary<string, byte[]>>(dictContentsName);
                await dictContents.AddOrUpdateAsync(tx, s3Object.Key, s3Object.Content, (k, v) => s3Object.Content, _defaultTimeout, cancellationToken);

                //Save the S3 object metadata as S3 entry
                var key = s3Object.Key;
                var parentKey = key.ParentDelimitedBy(PathDelimiter);
                var childKey = key.Substring(parentKey.Length == 1 ? 0 : parentKey.Length);
                var dictEntriesName = $"{S3EntriesKey}:{s3Object.Bucket}:{parentKey}";
                var dictEntries = await _stateManager.GetOrAddAsync<IReliableDictionary<string, S3Entry>>(dictEntriesName);
                {
                    var newEntry = ToS3Entry(s3Object);
                    await dictEntries.AddOrUpdateAsync(tx, childKey, newEntry, (k, v) =>
                    {
                        newEntry.CreationDate = v.CreationDate;
                        return newEntry;
                    }, _defaultTimeout, cancellationToken);
                }

                //Save the path hierarchy as S3 entries
                while (parentKey.Length > 1)
                {
                    key = parentKey;
                    parentKey = key.ParentDelimitedBy(PathDelimiter);
                    childKey = key.Substring(parentKey.Length == 1 ? 0 : parentKey.Length);
                    dictEntriesName = $"{S3EntriesKey}:{s3Object.Bucket}:{parentKey}";
                    dictEntries = await _stateManager.GetOrAddAsync<IReliableDictionary<string, S3Entry>>(dictEntriesName);
                    {
                        var newEntry = new S3Entry {CreationDate = s3Object.CreationDate};
                        await dictEntries.AddOrUpdateAsync(tx, childKey, newEntry, (k, v) => v, _defaultTimeout, cancellationToken);
                    }
                }
                
                await tx.CommitAsync();
            }
        }

        public async Task DeleteObjectAsync(string bucketId, string key)
        {
            await DeleteObjectAsync(bucketId, key, CancellationToken.None);
        }

        public async Task DeleteObjectAsync(string bucketId, string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(bucketId)) throw new InvalidOperationException("Bucket ID must not be empty!");
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Key must not be empty!");

            using (var tx = _stateManager.CreateTransaction())
            {
                //Remove the content value (byte array)
                var dictContentsName = $"{S3ContentsKey}:{bucketId}";
                var dictContents = await _stateManager.GetOrAddAsync<IReliableDictionary<string, byte[]>>(dictContentsName);
                await dictContents.TryRemoveAsync(tx, key, _defaultTimeout, cancellationToken);
                await tx.CommitAsync();
            }

            var parentKey = key.ParentDelimitedBy(PathDelimiter);
            var childKey = key.Substring(parentKey.Length == 1 ? 0 : parentKey.Length);
            var dictEntriesName = $"{S3EntriesKey}:{bucketId}:{parentKey}";

            //Cleanup S3 object metadata and path hierarchy (S3 entries and dictionaries - if empty)
            while (childKey.Length > 0)
            {
                var dictEntries = await _stateManager.TryGetAsync<IReliableDictionary<string, S3Entry>>(dictEntriesName);
                if (dictEntries.HasValue)
                {
                    long count = 0;
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        //Remove the S3 entry
                        await dictEntries.Value.TryRemoveAsync(tx, childKey, _defaultTimeout, cancellationToken);
                        await tx.CommitAsync();
                    }

                    using (var tx = _stateManager.CreateTransaction())
                    {
                        //Remove dictionary if empty
                        count = await dictEntries.Value.GetCountAsync(tx);
                        if (count < 1)
                        {
                            await _stateManager.RemoveAsync(tx, dictEntriesName);
                        }
                        await tx.CommitAsync();
                    }
                    if (count > 0) break;
                }
                key = parentKey;
                parentKey = key.ParentDelimitedBy(PathDelimiter);
                childKey = key.Substring(parentKey.Length == 1 ? 0 : parentKey.Length);
                dictEntriesName = $"{S3EntriesKey}:{bucketId}:{parentKey}";
            }
        }

        public async Task<S3Object> GetObjectAsync(string bucketId, string key)
        {
            return await GetObjectAsync(bucketId, key, CancellationToken.None);
        }

        public async Task<S3Object> GetObjectAsync(string bucketId, string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(bucketId)) throw new InvalidOperationException("Bucket ID must not be empty!");
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Key must not be empty!");

            using (var tx = _stateManager.CreateTransaction())
            {
                //Remove the content value (byte array)
                var dictContentsName = $"{S3ContentsKey}:{bucketId}";
                var dictContents = await _stateManager.TryGetAsync<IReliableDictionary<string, byte[]>>(dictContentsName);
                if (!dictContents.HasValue) return null;
                var content = await dictContents.Value.TryGetValueAsync(tx, key, _defaultTimeout, cancellationToken);
                if (!content.HasValue) return null;
                var parentKey = key.ParentDelimitedBy(PathDelimiter);
                var childKey = key.Substring(parentKey.Length == 1 ? 0 : parentKey.Length);
                var dictEntriesName = $"{S3EntriesKey}:{bucketId}:{parentKey}";
                var dictEntries = await _stateManager.TryGetAsync<IReliableDictionary<string, S3Entry>>(dictEntriesName);
                if (!dictEntries.HasValue) return null;
                var entry = await dictEntries.Value.TryGetValueAsync(tx, childKey, _defaultTimeout, cancellationToken);
                if (!entry.HasValue) return null;
                var s3Object = ToS3Object(entry.Value, bucketId, key);
                s3Object.Content = content.Value;
                return s3Object;
            }
        }

        public async Task<S3ObjectSearchResponse> SearchObjectsAsync(S3ObjectSearchRequest searchRequest)
        {
            return await SearchObjectsAsync(searchRequest, CancellationToken.None);
        }

        public async Task<S3ObjectSearchResponse> SearchObjectsAsync(S3ObjectSearchRequest searchRequest, CancellationToken cancellationToken)
        {
            var searchResponse = new S3ObjectSearchResponse
            {
                BucketName = searchRequest.BucketName,
                Marker = searchRequest.Marker,
                MaxKeys = searchRequest.MaxKeys,
                Delimiter = searchRequest.Delimiter,
                Prefix = searchRequest.Prefix,
                IsTruncated = false
            };

            using (var tx = _stateManager.CreateTransaction())
            {
                var key = searchRequest.Prefix;
                if (string.IsNullOrEmpty(key))
                {
                    if (string.IsNullOrEmpty(searchRequest.Delimiter)) return searchResponse;
                    key = $"{searchRequest.Delimiter}";
                }

                if (!key.EndsWith(PathDelimiter))
                {
                    var parentKey = key.ParentDelimitedBy(PathDelimiter);
                    var childKey = key.Substring(parentKey.Length == 1 ? 0 : parentKey.Length);
                    var dictEntriesName = $"{S3EntriesKey}:{searchRequest.BucketName}:{parentKey}";
                    var dictEntries = await _stateManager.TryGetAsync<IReliableDictionary<string, S3Entry>>(dictEntriesName);
                    if (!dictEntries.HasValue) return searchResponse;
                    var entry = await dictEntries.Value.TryGetValueAsync(tx, childKey, _defaultTimeout, cancellationToken);
                    if (!entry.HasValue) return searchResponse;
                    var s3Object = ToS3Object(entry.Value, searchRequest.BucketName, key);
                    searchResponse.S3Objects.Add(s3Object);
                    return searchResponse;
                }
                else
                {
                    var dictEntriesName = $"{S3EntriesKey}:{searchRequest.BucketName}:{key}";
                    var dictEntries = await _stateManager.TryGetAsync<IReliableDictionary<string, S3Entry>>(dictEntriesName);
                    if (!dictEntries.HasValue) return searchResponse;
                    var enumerator = (await dictEntries.Value.CreateEnumerableAsync(tx, EnumerationMode.Ordered)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(cancellationToken))
                    {
                        var childKey = enumerator.Current.Key;
                        if (childKey.EndsWith(PathDelimiter))
                        {
                            searchResponse.Prefixes.Add($"{searchRequest.Prefix}{childKey}");
                        }
                        else
                        {
                            var s3Object = ToS3Object(enumerator.Current.Value, searchRequest.BucketName, $"{searchRequest.Prefix}{childKey}");
                            searchResponse.S3Objects.Add(s3Object);
                        }
                    }
                }
            }

            return searchResponse;
        }

        #endregion Objects

        private static S3Entry ToS3Entry(S3Object s3Object)
        {
            var newEntry = new S3Entry
            {
                ContentType = s3Object.ContentType,
                CreationDate = s3Object.CreationDate,
                Hash = s3Object.ContentMD5,
                Size = s3Object.Size
            };
            return newEntry;
        }

        private static S3Object ToS3Object(S3Entry entry, string bucketId, string key)
        {
            var s3Object = new S3Object
            {
                Bucket = bucketId,
                Key = key,
                ContentType = entry.ContentType,
                CreationDate = entry.CreationDate,
                ContentMD5 = entry.Hash,
                Size = entry.Size
            };
            return s3Object;
        }
    }
}