using System;
using System.Buffers;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace DotNetTest.Controllers
{
    public class HomeController : Controller
    {
        private static readonly byte[] PayloadBytes = MakePayload();

        private ReadOnlySequence<byte> GetPayloadSequence() => new ReadOnlySequence<byte>(PayloadBytes);

        [HttpGet("/lease")]
        public ActionResult<string[]> Lease()
        {
            var payload = GetPayloadSequence();
            var lease = Lease<byte>.Create((int)payload.Length, false);
            payload.CopyTo(lease.Span);

            return new MemoryResult(lease, "application/json");
        }

        [HttpGet("/leaseETag")]
        public ActionResult<string[]> LeaseETag(string hashName = "SHA256")
        {
            var payload = GetPayloadSequence();
            var lease = Lease<byte>.Create((int)payload.Length, false);
            payload.CopyTo(lease.Span);

            return new MemoryResult(lease, "application/json")
            {
                ETagHashName = new HashAlgorithmName(hashName)
            };
        }

        [HttpGet("/array")]
        public ActionResult<string[]> Array()
        {
            var payload = GetPayloadSequence();
            var arr = payload.ToArray();

            return new FileContentResult(arr, "application/json");
        }

        private static byte[] MakePayload()
        {
            var items = Enumerable.Range(0, 10000).Select(i => new { number = i, @string = i.ToString() }).ToArray();
            var result = new { items };
            return JsonSerializer.SerializeToUtf8Bytes(result);
        }
    }
}
