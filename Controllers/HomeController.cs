using System;
using System.Buffers;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace DotNetTest.Controllers
{
    public class HomeController : Controller
    {
        private static readonly byte[] PayloadBytes = MakePayload();
        private static readonly DateTimeOffset Nowish = DateTimeOffset.UtcNow;

        private ReadOnlySequence<byte> GetPayloadSequence() => new ReadOnlySequence<byte>(PayloadBytes);
        private DateTimeOffset? LastModified => Request.QueryString.HasValue ? (DateTimeOffset?)Nowish : null;

        [HttpGet("/lease")]
        public ActionResult<string[]> Lease()
        {
            var payload = GetPayloadSequence();
            var lease = Lease<byte>.Create((int)payload.Length, false);
            payload.CopyTo(lease.Span);

            return new MemoryOwnerResult(lease, "application/json", LastModified);
        }

        [HttpGet("/array")]
        public ActionResult<string[]> Array()
        {
            var payload = GetPayloadSequence();
            var arr = payload.ToArray();

            return new FileContentResult(arr, "application/json")
            {
                LastModified = LastModified
            };
        }

        private static byte[] MakePayload()
        {
            var items = Enumerable.Range(0, 10000).Select(i => new { number = i, @string = i.ToString() }).ToArray();
            var result = new { items };
            return JsonSerializer.SerializeToUtf8Bytes(result);
        }
    }
}
