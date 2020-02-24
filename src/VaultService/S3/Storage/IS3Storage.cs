using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VaultService.S3.Model;
using VaultService.S3.Responses;

namespace VaultService.S3.Storage
{
    public interface IS3Storage
    {
        Task AddBucketAsync(Bucket bucket);
        Task AddBucketAsync(Bucket bucket, CancellationToken cancellationToken);
        Task DeleteBucketAsync(string bucketId);
        Task DeleteBucketAsync(string bucketId, CancellationToken cancellationToken);
        Task<Bucket> GetBucketAsync(string bucketId);
        Task<Bucket> GetBucketAsync(string bucketId, CancellationToken cancellationToken);
        Task<IEnumerable<Bucket>> ListBucketsAsync();
        Task<IEnumerable<Bucket>> ListBucketsAsync(CancellationToken cancellationToken);
        Task AddObjectAsync(S3Object s3Object);
        Task AddObjectAsync(S3Object s3Object, CancellationToken cancellationToken);
        Task DeleteObjectAsync(string bucketId, string key);
        Task DeleteObjectAsync(string bucketId, string key, CancellationToken cancellationToken);
        Task<S3Object> GetObjectAsync(string bucketId, string key);
        Task<S3Object> GetObjectAsync(string bucketId, string key, CancellationToken cancellationToken);
        Task<S3ObjectSearchResponse> SearchObjectsAsync(S3ObjectSearchRequest searchRequest);
        Task<S3ObjectSearchResponse> SearchObjectsAsync(S3ObjectSearchRequest searchRequest, CancellationToken cancellationToken);
    }
}
