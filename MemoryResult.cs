using System;
using System.Buffers;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace DotNetTest
{
    public class MemoryResult : ActionResult
    {
        public ReadOnlyMemory<byte> Memory { get; }
        public Action CleanUp { get; }
        public string ContentType { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public HashAlgorithmName? ETagHashName { get; set; }
        public EntityTagHeaderValue ETag { get; set; }

        public MemoryResult(IMemoryOwner<byte> memoryOwner, string contentType)
            : this(memoryOwner.Memory, contentType, memoryOwner.Dispose)
        {
        }

        public MemoryResult(ReadOnlyMemory<byte> memory, string contentType, Action cleanUp = null)
        {
            Memory = memory;
            ContentType = contentType;
            CleanUp = cleanUp;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            try
            {
                var requestHeaders = context.HttpContext.Request.GetTypedHeaders();
                var response = context.HttpContext.Response;
                var responseHeaders = response.GetTypedHeaders();

                if (LastModified.HasValue)
                {
                    responseHeaders.LastModified = LastModified;

                    var result = CheckLastModifiedPreconditions(requestHeaders);
                    if (result > 0)
                    {
                        response.StatusCode = result;
                        return;
                    }
                }

                if (ETag == null && ETagHashName.HasValue)
                {
                    ETag = MakeHashETag(ETagHashName.Value);
                }

                if (ETag != null)
                {
                    responseHeaders.ETag = ETag;

                    var result = CheckETagPreconditions(requestHeaders);
                    if (result > 0)
                    {
                        response.StatusCode = result;
                        return;
                    }
                }

                response.ContentType = ContentType;
                response.ContentLength = Memory.Length;
                await response.BodyWriter.WriteAsync(Memory, context.HttpContext.RequestAborted);
            }
            finally
            {
                CleanUp?.Invoke();
            }
        }

        public override void ExecuteResult(ActionContext context)
        {
            throw new NotImplementedException();
        }

        private int CheckLastModifiedPreconditions(RequestHeaders requestHeaders)
        {
            var ifModifiedSince = requestHeaders.IfModifiedSince;
            if (ifModifiedSince.HasValue && LastModified <= ifModifiedSince)
            {
                return StatusCodes.Status304NotModified;
            }

            var ifUnmodifiedSince = requestHeaders.IfUnmodifiedSince;
            if (ifUnmodifiedSince.HasValue && LastModified > ifUnmodifiedSince)
            {
                return StatusCodes.Status412PreconditionFailed;
            }

            return 0;
        }

        private int CheckETagPreconditions(RequestHeaders requestHeaders)
        {
            var ifNoneMatch = requestHeaders.IfNoneMatch;
            if (ifNoneMatch != null && ifNoneMatch.Any() && ifNoneMatch.Any(h => ETag.Compare(h, false)))
            {
                return StatusCodes.Status304NotModified;
            }

            var ifMatch = requestHeaders.IfMatch;
            if (ifMatch != null && ifMatch.Any() && !requestHeaders.IfMatch.Any(h => ETag.Compare(h, true)))
            {
                return StatusCodes.Status412PreconditionFailed;
            }

            return 0;
        }

        private EntityTagHeaderValue MakeHashETag(HashAlgorithmName hashName)
        {
            using var hasher = HashAlgorithm.Create(hashName.Name);

            var hashSizeInBytes = (int)Math.Ceiling(hasher.HashSize / 8.0);
            var buffer = new byte[hashSizeInBytes];

            var success = hasher.TryComputeHash(Memory.Span, buffer, out var bytesWritten);
            if (!success || bytesWritten != buffer.Length)
            {
                throw new Exception($"Expected ETag hash size to be {buffer.Length} bytes, but {bytesWritten} were written.");
            }

            var b64 = Convert.ToBase64String(buffer);
            return new EntityTagHeaderValue("\"" + b64 + "\"");
        }
    }
}
