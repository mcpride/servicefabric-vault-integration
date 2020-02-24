using System;
using Microsoft.Extensions.Options;
using Microsoft.ServiceFabric.Data.Collections;
using Moq;
using ServiceFabric.Mocks;
using VaultService.S3.Model;
using VaultService.S3.Storage;
using Xbehave;
using Xunit;

namespace VaultService.UnitTests.Units.ServiceFabricStorage
{
    public class GetBucketAsyncFeature
    {
        private const string S3BucketsKey = "S3:Buckets";
        private const string BucketId = "my-test-bucket";
        private MockReliableStateManager _stateManager;
        private IS3Storage _storage;

        [Background]
        public void Background()
        {
            $"Given a new ServiceFabricStorage instance"
                .x(() =>
                {
                    _stateManager = new MockReliableStateManager();
                    var optionsMock = new Mock<IOptionsSnapshot<ServiceFabricStorageOptions>>();
                    optionsMock.Setup(m => m.Value).Returns(new ServiceFabricStorageOptions
                        {DefaultTimeoutFromSeconds = 10});

                    _storage = new S3.Storage.ServiceFabricStorage(_stateManager, optionsMock.Object);
                });
        }

        [Scenario(DisplayName = "'Get Bucket' should return existing bucket entry")]
        public void Get_Bucket_should_return_existing_bucket_entry(Bucket bucket = null)
        {
            $"And an existing bucket entry with bucket id {BucketId} in reliable dictionary {S3BucketsKey}"
                .x(async () =>
                {
                    var dictionary =
                        await _stateManager.GetOrAddAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        var newBucket = new Bucket {Id = BucketId, CreationDate = DateTime.UtcNow};
                        await dictionary.AddAsync(tx, BucketId, newBucket);
                        await tx.CommitAsync();
                    }
                });

            $"When GetBucketAsync() be called with bucket id = {BucketId}"
                .x(async () => bucket = await _storage.GetBucketAsync(BucketId));

            $"Then the existing bucket entry with bucket id {BucketId} is returned"
                .x(() => Assert.Equal(BucketId, bucket.Id));
        }

        [Scenario(DisplayName = "'GetBucket' with unknown bucket id should return null")]
        [Example("bla")]
        [Example("bla-bla")]
        public void GetBucket_with_unknown_bucket_id_should_return_null(string bucketId, Bucket bucket = null)
        {
            "When GetBucketAsync() is called with unknown bucket id"
                .x(async () => bucket = await _storage.GetBucketAsync(bucketId));
            "Then the result is null"
                .x(() => Assert.Null(bucket));
        }

        [Scenario(DisplayName = "'GetBucket' with bucket id == null should return null")]
        public void GetBucket_with_bucket_id_null_should_return_null(string bucketId = null, Bucket bucket = null)
        {
            "When GetBucketAsync() is called with bucket id == null"
                .x(async () => bucket = await _storage.GetBucketAsync(bucketId));
            "Then the result is null"
                .x(() => Assert.Null(bucket));
        }
    }
}