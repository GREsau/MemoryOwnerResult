using System;
using System.Buffers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTest
{
    public class MemoryOwnerResult : ActionResult
    {
        private readonly IMemoryOwner<byte> _memoryOwner;
        private readonly string _contentType;
        private readonly DateTimeOffset? _lastModified;

        public MemoryOwnerResult(IMemoryOwner<byte> memoryOwner, string contentType, DateTimeOffset? lastModified = null)
        {
            _memoryOwner = memoryOwner;
            _contentType = contentType;
            _lastModified = lastModified;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;

            using (_memoryOwner)
            {
                var ifModifiedSince = context.HttpContext.Request.GetTypedHeaders().IfModifiedSince;
                if (_lastModified.HasValue && ifModifiedSince.HasValue && _lastModified <= ifModifiedSince)
                {
                    response.StatusCode = StatusCodes.Status304NotModified;
                }

                var memory = _memoryOwner.Memory;

                response.ContentType = _contentType;
                response.ContentLength = memory.Length;
                if (_lastModified.HasValue)
                {
                    response.GetTypedHeaders().LastModified = _lastModified;
                }

                await response.BodyWriter.WriteAsync(memory, context.HttpContext.RequestAborted);
            }
        }

        public override void ExecuteResult(ActionContext context)
        {
            throw new NotImplementedException();
        }
    }
}
