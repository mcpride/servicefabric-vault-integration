using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VaultService.Core.Extensions;
using VaultService.S3.Model;
using VaultService.S3.Responses;
using VaultService.S3.Storage;

namespace VaultService.S3.Controllers
{
    [Route("")]
    [ApiController]
    public class S3Controller : ControllerBase
    {
        private readonly IS3Storage _storage;
        private readonly IS3Responder _responder;
        private readonly ILogger _logger;

        public S3Controller(IS3Storage storage, IS3Responder responder, ILoggerFactory loggerFactory)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<S3Controller>();
        }

        [HttpGet]
        [Route("{bucket}/{*key}")]
        public async Task<ActionResult> Get(string bucket, string key, CancellationToken cancellationToken)
        {
            var s3Object = await _storage.GetObjectAsync(bucket, key, cancellationToken);
            if (s3Object == null)
            {
                return NotFound();
            }
            Response.Headers.Add("ETag", $"\"{s3Object.ContentMD5}\"");
            if (s3Object.Content != null && s3Object.Content.Length > 0)
            {
                return File(s3Object.Content, string.IsNullOrEmpty(s3Object.ContentType) ? "application/octet-stream" : s3Object.ContentType);
            }
            return Ok();
        }

        [HttpPut]
        [Route("{bucket}/{*key}")]
        public async Task<IActionResult> Put(string bucket, string key, CancellationToken cancellationToken)
        {
            if (Request.QueryString.HasValue && Request.QueryString.Value == "?acl")
            {
                return Ok();
            }

            var s3Object = new S3Object
            {
                Bucket = bucket,
                Key = key,
                ContentType = string.IsNullOrEmpty(Request.ContentType) ? "application/octet-stream" : Request.ContentType,
                CreationDate = DateTime.UtcNow,
                Size = 0
            };
            if (Request.Body != null)
            {
                s3Object.Content =  await Request.Body.ReadAllBytesAsync();
            }

            await _storage.AddObjectAsync(s3Object, cancellationToken);

            var response = Ok();
            Response.Headers.Add("ETag", $"\"{s3Object.ContentMD5}\"");
            return response;
        }

        [HttpDelete]
        [Route("{bucket}/{*key}")]
        public async Task<IActionResult> Delete(string bucket, string key, CancellationToken cancellationToken)
        {
            await _storage.DeleteObjectAsync(bucket, key, cancellationToken);
            return NoContent();
        }

        [HttpHead]
        [Route("{bucket}")]
        public async Task<IActionResult> Head(string bucket, CancellationToken cancellationToken)
        {
            var bucketObject = await _storage.GetBucketAsync(bucket, cancellationToken);
            if (bucketObject != null) return Ok();
            var responseNotFound = _responder.RespondContent(new BucketNotFound { BucketName = bucket });
            responseNotFound.StatusCode = (int)HttpStatusCode.NotFound;
            return responseNotFound;
        }

        [HttpGet]
        [Route("{bucket}")]
        public async Task<IActionResult> Get(string bucket, CancellationToken cancellationToken)
        {
            if (Request.QueryString.HasValue && Request.QueryString.Value == "?acl")
            {
                return _responder.RespondContent(new ACLRequest());
            }
            return await ListObjectsAsync(bucket, cancellationToken);
        }

        [HttpPut]
        [Route("{bucket}")]
        public async Task<IActionResult> Put(string bucket, CancellationToken cancellationToken)
        {
            var newBucket = new Bucket { Id = bucket, CreationDate = DateTime.UtcNow };
            await _storage.AddBucketAsync(newBucket, cancellationToken);
            return Ok();
        }

        [HttpDelete]
        [Route("{bucket}")]
        public async Task<IActionResult> Delete(string bucket, CancellationToken cancellationToken)
        {
            await _storage.DeleteBucketAsync(bucket, cancellationToken);
            return NoContent();
        }

        [HttpPost]
        [Route("{bucket}")]
        public async Task<IActionResult> Post(string bucket, CancellationToken cancellationToken)
        {
            if (Request.QueryString.HasValue && Request.QueryString.Value == "?delete")
            {
                var serializer = new XmlSerializer(typeof(DeleteRequest));
                var deleteRequest = (DeleteRequest)serializer.Deserialize(Request.Body);
                await _storage.DeleteObjectAsync(bucket, deleteRequest.Object.Key, cancellationToken);
                return _responder.RespondContent(deleteRequest);
            }
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var bucketList = await _storage.ListBucketsAsync(cancellationToken);
            return _responder.RespondContent(bucketList);
        }

        private async Task<IActionResult> ListObjectsAsync(string bucket, CancellationToken cancellationToken)
        {
            var bucketObject = await _storage.GetBucketAsync(bucket, cancellationToken);
            if (bucketObject == null)
            {
                var responseNotFound = _responder.RespondContent(new BucketNotFound { BucketName = bucket });
                responseNotFound.StatusCode = (int)HttpStatusCode.NotFound;
                return responseNotFound;
            }

            var searchRequest = new S3ObjectSearchRequest
            {
                BucketName = bucket,
                Prefix = Request.Query.ContainsKey("prefix") ? Request.Query["prefix"][0] : string.Empty,
                Delimiter = Request.Query.ContainsKey("delimiter") ? Request.Query["delimiter"][0] : string.Empty,
                Marker = Request.Query.ContainsKey("marker") ? Request.Query["marker"][0] : string.Empty,
            };

            var searchResponse = await _storage.SearchObjectsAsync(searchRequest, cancellationToken);
            var response = _responder.RespondContent(searchResponse);
#if DEBUG
            if (_logger.IsEnabled(LogLevel.Information))
            {
#else
            if (_logger.IsEnabled(LogLevel.Trace))
            {
#endif
#if DEBUG
                _logger.LogInformation
#else
                _logger.LogTrace
#endif
                ("GET Request: bucket='{0}' query='{1}' response='{2}'",
                    bucket,
                    Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty,
                    string.IsNullOrEmpty(response.Content) ? string.Empty : response.Content);
            }

            return response;
        }
    }
}
