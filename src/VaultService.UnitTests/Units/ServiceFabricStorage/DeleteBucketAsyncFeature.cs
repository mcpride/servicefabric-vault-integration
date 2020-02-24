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
    public class DeleteBucketAsyncFeature
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

        [Scenario(DisplayName = "Deletion of an existing bucket should remove existing bucket entry")]
        public void Deletion_of_an_existing_bucket_should_remove_existing_bucket_entry(string bucketId = null)
        {
            $"And a bucket id {BucketId}"
                .x(() => bucketId = BucketId);

            $"And an existing bucket entry with bucket id {BucketId} in reliable dictionary {S3BucketsKey}"
                .x(async () =>
                {
                    var dictionary =
                        await _stateManager.GetOrAddAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        var bucket = new Bucket {Id = bucketId, CreationDate = DateTime.UtcNow};
                        await dictionary.AddAsync(tx, bucketId, bucket);
                        await tx.CommitAsync();
                    }
                });

            $"When DeleteBucketAsync() will be called with bucket id = {BucketId}"
                .x(async () => await _storage.DeleteBucketAsync(bucketId));

            $"Then the reliable dictionary {S3BucketsKey} does not contain an entry with these bucket id"
                .x(async () =>
                {
                    var dictionary = await _stateManager.TryGetAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        var exists = await dictionary.Value.ContainsKeyAsync(tx, bucketId);
                        Assert.False(exists);
                    }
                });
        }

        [Scenario(DisplayName = "Deletion with unknown bucket id should not fail")]
        [Example("bla")]
        [Example("bla-bla")]
        public void Deletion_with_unknown_bucket_id_should_not_fail(string bucketId, Exception exception = null)
        {
            "When DeleteBucketAsync() is called with bucket id"
                .x(async () =>
                    exception = await Record.ExceptionAsync(async () => await _storage.DeleteBucketAsync(bucketId)));
            "Then no exception is thrown"
                .x(() => Assert.Null(exception));
        }

        [Scenario(DisplayName = "Deletion with bucket id == null should not fail")]
        public void Deletion_with_bucket_id_null_should_not_fail(string bucketId = null, Exception exception = null)
        {
            "When DeleteBucketAsync() is called with bucket id  == null"
                .x(async () =>
                    exception = await Record.ExceptionAsync(async () => await _storage.DeleteBucketAsync(bucketId)));
            "Then no exception is thrown"
                .x(() => Assert.Null(exception));
        }
    }
}