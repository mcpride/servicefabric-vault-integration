using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Options;
using Microsoft.ServiceFabric.Data.Collections;
using Moq;
using ServiceFabric.Mocks;
using VaultService.Extensions;
using VaultService.S3.Model;
using VaultService.S3.Storage;
using Xbehave;
using Xunit;

namespace VaultService.UnitTests.Units.ServiceFabricStorage
{
    public class DeleteObjectAsyncFeature
    {
        private const string S3EntriesKey = "S3:Entries";
        private const string S3ContentsKey = "S3:Contents";
        private const string BucketId = "my-test-bucket";
        private const char PathDelimiter = '/';
        private MockReliableStateManager _stateManager;
        private IS3Storage _storage;

        [Background]
        public void Background()
        {
            "Given a new ServiceFabricStorage instance"
                .x(() =>
                {
                    _stateManager = new MockReliableStateManager();
                    var optionsMock = new Mock<IOptionsSnapshot<ServiceFabricStorageOptions>>();
                    optionsMock.Setup(m => m.Value).Returns(new ServiceFabricStorageOptions {  DefaultTimeoutFromSeconds = 10 });
                    _storage = new S3.Storage.ServiceFabricStorage(_stateManager, optionsMock.Object);
                });
            $"And a saved Bucket named {BucketId}"
                .x(async () =>
                {
                    await _storage.AddBucketAsync(new Bucket { CreationDate = DateTime.UtcNow, Id = BucketId });
                });
        }

        [Scenario(DisplayName = "Remove previously saved S3 object")]
        [Example("rootpath/middlepath/childpath")]
        //[Example("rootpath/middlepath")]
        //[Example("rootpath")]
        public void Remove_previously_saved_S3_object(string s3ObjectKey, S3Object s3Object = null)
        {
            "And a S3 Object with given key is saved"
                .x(async () =>
                    {
                        s3Object = new S3Object
                        {
                            Bucket = BucketId,
                            Key = s3ObjectKey,
                            CreationDate = DateTime.UtcNow,
                            Content = new byte[] {0x01, 0x02, 0x03},
                            ContentType = "application/octet-stream"
                        };
                        await _storage.AddObjectAsync(s3Object);
                    });

            "When method RemoveS3ObjectAsync() will be called with given key"
                .x(async () =>  await _storage.DeleteObjectAsync(BucketId, s3ObjectKey));

            "Then the corresponding reliable dictionaries are removed"
                .x(async () =>
                {
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        var uriPrefix = $"fabric://mocks/{S3EntriesKey}:{s3Object.Bucket}:";
                        var enumerator = _stateManager.GetAsyncEnumerator();
                        while (await enumerator.MoveNextAsync(CancellationToken.None))
                        {
                            var uriStr = enumerator.Current.Name.ToString();
                            Assert.False(uriStr.StartsWith(uriPrefix));
                        }
                    }
                });
        }
    }
}