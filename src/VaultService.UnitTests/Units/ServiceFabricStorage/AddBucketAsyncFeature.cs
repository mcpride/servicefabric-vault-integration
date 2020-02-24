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
    public class AddBucketAsyncFeature
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
                    optionsMock.Setup(m => m.Value).Returns(new ServiceFabricStorageOptions {  DefaultTimeoutFromSeconds = 10 });

                    _storage = new S3.Storage.ServiceFabricStorage(_stateManager, optionsMock.Object);
                });
        }

        [Scenario(DisplayName = "Calling method AddBucketAsync with null as bucket parameter should fail")]
        public void Calling_method_AddBucketAsync_with_null_as_bucket_parameter_should_fail(Exception exception = null)
        {
            $"When method AddBucketAsync method will be called with parameter bucket == null"
                .x(async() => exception = await Record.ExceptionAsync(async() => await _storage.AddBucketAsync(null)));

            $"Then an ArgumentNullException is thrown"
                .x(() => Assert.IsType<ArgumentNullException>(exception));
        }

        [Scenario(DisplayName = "Adding new bucket adds new entry to reliable dictionary")]
        public void Adding_new_bucket_adds_new_entry_to_reliable_dictionary(Bucket bucket = null)
        {
            $"Given a new bucket"
                .x(() => bucket = new Bucket{ Id = BucketId, CreationDate = DateTime.UtcNow});

            $"When method AddBucketAsync() will be called with new bucket"
                .x(async () => await _storage.AddBucketAsync(bucket));

            $"Then the reliable dictionary {S3BucketsKey} contains an entry with these bucket"
                .x(async () =>
                {
                    var dictionary = await _stateManager.TryGetAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        var exists = await dictionary.Value.ContainsKeyAsync(tx, bucket.Id);
                        Assert.True(exists);
                    }
                });
        }

        [Scenario(DisplayName = "Adding new bucket with same id as previously saved bucket should not exchange the older one")]
        public void Adding_new_bucket_with_same_id_as_previously_saved_bucket_should_not_exchange_the_older_one(
            Bucket savedBucket = null, Bucket newBucket = null)
        {
            $"And a bucket with ID {BucketId} saved in reliable dictionary {S3BucketsKey}"
                .x(async () =>
                {
                    savedBucket = new Bucket {Id = BucketId, CreationDate = DateTime.UtcNow.AddHours(-1) };
                    var dictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        await dictionary.AddAsync(tx, savedBucket.Id, savedBucket);
                        await tx.CommitAsync();
                    }
                });

            $"And a new bucket with same ID {BucketId} but different creation time"
                .x(() => newBucket = new Bucket { Id = BucketId, CreationDate = DateTime.UtcNow.AddHours(1) });

            "When method AddBucketAsync() will be called with this new bucket"
                .x(async () => await _storage.AddBucketAsync(newBucket));

            $"Then the reliable dictionary {S3BucketsKey} contains a bucket entry with the creation date of the former saved bucket"
                .x(async () =>
                {
                    var dictionary = await _stateManager.TryGetAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        var bucket = await dictionary.Value.TryGetValueAsync(tx, newBucket.Id);
                        Assert.True(bucket.HasValue);
                        Assert.Equal(savedBucket.CreationDate, bucket.Value.CreationDate);
                        Assert.NotEqual(newBucket.CreationDate, bucket.Value.CreationDate);
                    }
                });
        }
    }
}