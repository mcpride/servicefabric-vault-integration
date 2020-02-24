using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public class ListBucketAsyncFeature
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

        [Scenario(DisplayName = "'List Buckets' should return all existing bucket entries")]
        [Example(new object[] { new string[] {} })]
        [Example(new object[] { new[] { "bucket1" } })]
        [Example(new object[] { new[] { "bucket1", "bucket2" } })]
        [Example(new object[] { new[] {"bucket1", "bucket2", "bucket3" } })]
        public void List_Buckets_should_return_all_existing_bucket_entries(string[] bucketIds)
        {
            IEnumerable<Bucket> buckets = null;

            $"And existing bucket entries in reliable dictionary {S3BucketsKey}"
                .x(async () =>
                {
                    var dictionary =
                        await _stateManager.GetOrAddAsync<IReliableDictionary<string, Bucket>>(S3BucketsKey);
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        foreach (var bucketId in bucketIds)
                        {
                            var newBucket = new Bucket { Id = bucketId, CreationDate = DateTime.UtcNow };
                            await dictionary.AddAsync(tx, bucketId, newBucket);
                        }
                        await tx.CommitAsync();
                    }
                });

            "When ListBucketsAsync() be called"
                .x(async () => buckets = await _storage.ListBucketsAsync());

            "Then all existing bucket entries are returned"
                .x(() => Assert.Equal(bucketIds.Length, buckets.Count()));
        }
    }
}