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
    public class AddObjectAsyncFeature
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

        [Scenario(DisplayName = "Adding new S3 object adds new s3 entry to reliable dictionary")]
        [Example("rootpath/middlepath/childpath")]
        [Example("rootpath/middlepath")]
        [Example("rootpath")]
        public void Adding_new_S3Object_adds_new_S3Entry_to_reliable_dictionary(string s3ObjectKey, S3Object s3Object = null, IList<string> entriesDictionaryNames = null)
        {
            "And a S3 Object with given key"
                .x(() =>
                    {
                        s3Object = new S3Object
                        {
                            Bucket = BucketId,
                            Key = s3ObjectKey,
                            CreationDate = DateTime.UtcNow,
                            Content = new byte[] {0x01, 0x02, 0x03},
                            ContentType = "application/octet-stream"
                        };
                    });

            "When method AddS3ObjectAsync() will be called with new S3 object"
                .x(async () =>  await _storage.AddObjectAsync(s3Object));

            "Then the reliable dictionary contains a content entry for these S3 object"
                .x(async () =>
                {
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        var dictContentsName = $"{S3ContentsKey}:{s3Object.Bucket}";
                        var dictContents = await _stateManager.TryGetAsync<IReliableDictionary<string, byte[]>>(dictContentsName);
                        var exists = await dictContents.Value.ContainsKeyAsync(tx, s3Object.Key);
                        Assert.True(exists);
                    }
                });

            "And the reliable dictionary contains a metadata entry for these S3 object"
                .x(async () =>
                {
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        var key = s3Object.Key;
                        var parentKey = key.ParentDelimitedBy(PathDelimiter);
                        var childKey = key.Substring(parentKey.Length == 1 ? 0 : parentKey.Length);
                        var dictEntriesName = $"{S3EntriesKey}:{s3Object.Bucket}:{parentKey}";
                        var dictEntries = await _stateManager.TryGetAsync<IReliableDictionary<string, S3.Storage.ServiceFabricStorage.S3Entry>>(dictEntriesName);
                        var exists = await dictEntries.Value.ContainsKeyAsync(tx, childKey);
                        Assert.True(exists);
                    }
                });
            "And the corresponding reliable dictionaries for the path entries are available"
                .x(async () =>
                {
                    //1 content dictionary; 1 bucket dictionary; 1 meta dictionary; n-1 parent path dictionaries

                    var segments = s3Object.Key.DelimitedBy(PathDelimiter);
                    var excludeKey = s3Object.Key.ParentDelimitedBy(PathDelimiter);
                    var expectedDictCount = segments.Length - 1; 
                    var uriPrefix = $"fabric://mocks/{S3EntriesKey}:{s3Object.Bucket}:";
                    var enumerator = _stateManager.GetAsyncEnumerator();
                    entriesDictionaryNames = new List<string>();
                    while (await enumerator.MoveNextAsync(CancellationToken.None))
                    {
                        var uriStr = enumerator.Current.Name.ToString();
                        if (uriStr.StartsWith(uriPrefix) || uriStr.EndsWith(PathDelimiter))
                        {
                            var key = uriStr.Substring(uriPrefix.Length);
                            if (key.Equals(excludeKey)) continue; //already tested in previous step
                            entriesDictionaryNames.Add($"{S3EntriesKey}:{s3Object.Bucket}:{key}");
                        }
                    }
                    Assert.Equal(expectedDictCount, entriesDictionaryNames.Count);
                });
            "And these corresponding reliable dictionaries for the path entries contains valid entries"
                .x(async () =>
                {
                    using (var tx = _stateManager.CreateTransaction())
                    {
                        foreach (var dictionaryName in entriesDictionaryNames)
                        {
                            var dictionary = await _stateManager.TryGetAsync<IReliableDictionary<string, S3.Storage.ServiceFabricStorage.S3Entry>>(dictionaryName);
                            var enumerator = (await dictionary.Value.CreateEnumerableAsync(tx, EnumerationMode.Unordered)).GetAsyncEnumerator();
                            var count = 0;
                            while (await enumerator.MoveNextAsync(CancellationToken.None))
                            {
                                count++;
                                Assert.NotNull(enumerator.Current.Value);
                                Assert.False(enumerator.Current.Key.StartsWith(PathDelimiter));
                                Assert.True(enumerator.Current.Key.EndsWith(PathDelimiter));
                            }

                            Assert.Equal(1, count);
                        }
                    }
                });
        }
    }
}