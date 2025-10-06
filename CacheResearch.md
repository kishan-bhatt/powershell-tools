// Project: MyCompany.CacheApi
// Purpose: RESTful API endpoints for Redis-backed cache utility.
// Structure: Controller, DTOs, Service, Repository (interface), Middleware, and Unit Tests.

// =========================
// File: Controllers/CacheController.cs
// =========================
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyCompany.CacheApi.Services;
using MyCompany.CacheApi.DTOs;

namespace MyCompany.CacheApi.Controllers
{
    [ApiController]
    [Route("api/companies/{companyId:guid}/cache")]
    public class CacheController : ControllerBase
    {
        private readonly ICacheService _cacheService;

        public CacheController(ICacheService cacheService)
        {
            _cacheService = cacheService;
        }

        // GET: api/companies/{companyId}/cache?officeNumber=123&key=abc
        [HttpGet]
        public async Task<IActionResult> GetCache([FromRoute] Guid companyId, [FromQuery] string? officeNumber, [FromQuery] string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "key is required" });

            var item = await _cacheService.GetAsync(companyId, officeNumber, key);
            if (item is null)
                return NotFound();

            return Ok(item);
        }

        // POST: api/companies/{companyId}/cache
        // Body: { key, value, ttlSeconds, officeNumber }
        [HttpPost]
        public async Task<IActionResult> SetCache([FromRoute] Guid companyId, [FromBody] SetCacheRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _cacheService.SetAsync(companyId, request.OfficeNumber, request.Key, request.Value, request.TtlSeconds);
            return Ok(new { success = true });
        }

        // PUT: api/companies/{companyId}/cache/{key}
        [HttpPut("{key}")]
        public async Task<IActionResult> UpdateCache([FromRoute] Guid companyId, [FromRoute] string key, [FromBody] UpdateCacheRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = await _cacheService.UpdateAsync(companyId, request.OfficeNumber, key, request.Value, request.TtlSeconds);
            if (!updated)
                return NotFound();

            return Ok(new { success = true });
        }

        // DELETE: api/companies/{companyId}/cache/{key}?officeNumber=123
        [HttpDelete("{key}")]
        public async Task<IActionResult> DeleteCache([FromRoute] Guid companyId, [FromRoute] string key, [FromQuery] string? officeNumber)
        {
            var deleted = await _cacheService.DeleteAsync(companyId, officeNumber, key);
            if (!deleted)
                return NotFound();

            return Ok(new { success = true });
        }

        // POST: api/companies/{companyId}/cache/invalidate
        // Body: { officeNumber, pattern }
        [HttpPost("invalidate")]
        public async Task<IActionResult> InvalidateCache([FromRoute] Guid companyId, [FromBody] InvalidateCacheRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _cacheService.InvalidateAsync(companyId, request.OfficeNumber, request.Pattern);
            return Ok(new { success = true });
        }
    }
}


// =========================
// File: DTOs/SetCacheRequest.cs
// =========================
using System.ComponentModel.DataAnnotations;

namespace MyCompany.CacheApi.DTOs
{
    public class SetCacheRequest
    {
        [Required]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        // optional: time-to-live in seconds
        public int? TtlSeconds { get; set; }

        // optional office number used to scope cache
        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/UpdateCacheRequest.cs
// =========================
using System.ComponentModel.DataAnnotations;

namespace MyCompany.CacheApi.DTOs
{
    public class UpdateCacheRequest
    {
        [Required]
        public string Value { get; set; } = string.Empty;

        public int? TtlSeconds { get; set; }

        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/InvalidateCacheRequest.cs
// =========================
using System.ComponentModel.DataAnnotations;

namespace MyCompany.CacheApi.DTOs
{
    public class InvalidateCacheRequest
    {
        // If pattern is null/empty + officeNumber null -> invalidate everything for company
        public string? Pattern { get; set; }

        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/CacheItemDto.cs
// =========================
namespace MyCompany.CacheApi.DTOs
{
    public class CacheItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public long? TtlSeconds { get; set; }
        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: Services/ICacheService.cs
// =========================
using System;
using System.Threading.Tasks;
using MyCompany.CacheApi.DTOs;

namespace MyCompany.CacheApi.Services
{
    public interface ICacheService
    {
        Task<CacheItemDto?> GetAsync(Guid companyId, string? officeNumber, string key);
        Task SetAsync(Guid companyId, string? officeNumber, string key, string value, int? ttlSeconds);
        Task<bool> UpdateAsync(Guid companyId, string? officeNumber, string key, string value, int? ttlSeconds);
        Task<bool> DeleteAsync(Guid companyId, string? officeNumber, string key);
        Task InvalidateAsync(Guid companyId, string? officeNumber, string? pattern);
    }
}


// =========================
// File: Services/CacheService.cs
// =========================
using System;
using System.Threading.Tasks;
using MyCompany.CacheApi.DTOs;

namespace MyCompany.CacheApi.Services
{
    public class CacheService : ICacheService
    {
        private readonly IRedisRepository _repo;

        public CacheService(IRedisRepository repo)
        {
            _repo = repo;
        }

        public async Task<CacheItemDto?> GetAsync(Guid companyId, string? officeNumber, string key)
        {
            var redisKey = BuildKey(companyId, officeNumber, key);
            var value = await _repo.GetStringAsync(redisKey);
            if (value is null) return null;

            var ttl = await _repo.GetTtlSecondsAsync(redisKey);
            return new CacheItemDto { Key = key, Value = value, TtlSeconds = ttl, OfficeNumber = officeNumber };
        }

        public async Task SetAsync(Guid companyId, string? officeNumber, string key, string value, int? ttlSeconds)
        {
            var redisKey = BuildKey(companyId, officeNumber, key);
            await _repo.SetStringAsync(redisKey, value, ttlSeconds);
        }

        public async Task<bool> UpdateAsync(Guid companyId, string? officeNumber, string key, string value, int? ttlSeconds)
        {
            var redisKey = BuildKey(companyId, officeNumber, key);
            var exists = await _repo.ExistsAsync(redisKey);
            if (!exists) return false;

            await _repo.SetStringAsync(redisKey, value, ttlSeconds);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid companyId, string? officeNumber, string key)
        {
            var redisKey = BuildKey(companyId, officeNumber, key);
            return await _repo.DeleteKeyAsync(redisKey);
        }

        public async Task InvalidateAsync(Guid companyId, string? officeNumber, string? pattern)
        {
            // pattern can be null => remove all keys for companyId (and maybe officeNumber)
            var prefix = BuildPrefix(companyId, officeNumber);
            var finalPattern = string.IsNullOrEmpty(pattern) ? "*" : pattern;
            await _repo.DeleteByPatternAsync(prefix + finalPattern);
        }

        private static string BuildKey(Guid companyId, string? officeNumber, string key)
        {
            return string.IsNullOrWhiteSpace(officeNumber)
                ? $"company:{companyId}:{key}"
                : $"company:{companyId}:office:{officeNumber}:{key}";
        }

        private static string BuildPrefix(Guid companyId, string? officeNumber)
        {
            return string.IsNullOrWhiteSpace(officeNumber)
                ? $"company:{companyId}:"
                : $"company:{companyId}:office:{officeNumber}:";
        }
    }
}


// =========================
// File: Repositories/IRedisRepository.cs
// =========================
using System.Threading.Tasks;

namespace MyCompany.CacheApi
{
    public interface IRedisRepository
    {
        Task<string?> GetStringAsync(string key);
        Task SetStringAsync(string key, string value, int? ttlSeconds);
        Task<long?> GetTtlSecondsAsync(string key);
        Task<bool> ExistsAsync(string key);
        Task<bool> DeleteKeyAsync(string key);
        Task DeleteByPatternAsync(string pattern);
    }
}


// =========================
// File: Middleware/ExceptionHandlingMiddleware.cs
// =========================
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MyCompany.CacheApi.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var code = HttpStatusCode.InternalServerError;
            var result = JsonSerializer.Serialize(new { error = "An unexpected error occurred." });

            // optional: map custom exceptions to different status codes

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)code;
            return context.Response.WriteAsync(result);
        }
    }
}


// =========================
// File: Startup / Program.cs (snippet to wire up dependencies and middleware)
// =========================
/*
// In Program.cs (minimal hosting model)
var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Replace with your concrete RedisRepository implementation
builder.Services.AddSingleton<IRedisRepository, StackExchangeRedisRepository>();
builder.Services.AddScoped<ICacheService, CacheService>();

var app = builder.Build();

app.UseMiddleware<MyCompany.CacheApi.Middleware.ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();
*/


// =========================
// File: Repositories/StackExchangeRedisRepository.cs (outline)
// =========================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace MyCompany.CacheApi
{
    public class StackExchangeRedisRepository : IRedisRepository
    {
        private readonly IDatabase _db;
        private readonly IConnectionMultiplexer _mux;

        public StackExchangeRedisRepository(IConnectionMultiplexer mux)
        {
            _mux = mux;
            _db = mux.GetDatabase();
        }

        public async Task<string?> GetStringAsync(string key)
        {
            var v = await _db.StringGetAsync(key).ConfigureAwait(false);
            return v.IsNull ? null : v.ToString();
        }

        public async Task SetStringAsync(string key, string value, int? ttlSeconds)
        {
            TimeSpan? expiry = ttlSeconds.HasValue ? TimeSpan.FromSeconds(ttlSeconds.Value) : (TimeSpan?)null;
            await _db.StringSetAsync(key, value, expiry).ConfigureAwait(false);
        }

        public async Task<long?> GetTtlSecondsAsync(string key)
        {
            var ttl = await _db.KeyTimeToLiveAsync(key).ConfigureAwait(false);
            return ttl?.Ticks is null ? null : (long?)ttl?.TotalSeconds;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _db.KeyExistsAsync(key).ConfigureAwait(false);
        }

        public async Task<bool> DeleteKeyAsync(string key)
        {
            return await _db.KeyDeleteAsync(key).ConfigureAwait(false);
        }

        // Note: SCAN-like pattern deletion requires iterating server keys - be careful in production.
        public async Task DeleteByPatternAsync(string pattern)
        {
            foreach (var endPoint in _mux.GetEndPoints())
            {
                var server = _mux.GetServer(endPoint);
                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Length == 0) continue;
                await _db.KeyDeleteAsync(keys).ConfigureAwait(false);
            }
        }
    }
}


// =========================
// File: Tests/CacheControllerTests.cs (xUnit + Moq unit tests)
// =========================
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MyCompany.CacheApi.Controllers;
using MyCompany.CacheApi.DTOs;
using MyCompany.CacheApi.Services;
using Xunit;

namespace MyCompany.CacheApi.Tests
{
    public class CacheControllerTests
    {
        private readonly Mock<ICacheService> _service = new();

        [Fact]
        public async Task GetCache_ReturnsNotFound_WhenMissing()
        {
            var controller = new CacheController(_service.Object);
            var companyId = Guid.NewGuid();
            _service.Setup(s => s.GetAsync(companyId, It.IsAny<string?>(), "key1")).ReturnsAsync((CacheItemDto?)null);

            var result = await controller.GetCache(companyId, null, "key1");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetCache_ReturnsOk_WhenFound()
        {
            var controller = new CacheController(_service.Object);
            var companyId = Guid.NewGuid();
            var dto = new CacheItemDto { Key = "k", Value = "v" };
            _service.Setup(s => s.GetAsync(companyId, null, "k")).ReturnsAsync(dto);

            var result = await controller.GetCache(companyId, null, "k");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task SetCache_ReturnsBadRequest_WhenModelInvalid()
        {
            var controller = new CacheController(_service.Object);
            controller.ModelState.AddModelError("Key", "Required");
            var result = await controller.SetCache(Guid.NewGuid(), new SetCacheRequest());
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetCache_CallsServiceAndReturnsOk()
        {
            var companyId = Guid.NewGuid();
            var req = new SetCacheRequest { Key = "k", Value = "v" };
            var controller = new CacheController(_service.Object);
            _service.Setup(s => s.SetAsync(companyId, null, "k", "v", null)).Returns(Task.CompletedTask);

            var result = await controller.SetCache(companyId, req);

            _service.Verify(s => s.SetAsync(companyId, null, "k", "v", null), Times.Once);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task UpdateCache_ReturnsNotFound_WhenMissing()
        {
            var companyId = Guid.NewGuid();
            var controller = new CacheController(_service.Object);
            _service.Setup(s => s.UpdateAsync(companyId, null, "k", "v", null)).ReturnsAsync(false);

            var result = await controller.UpdateCache(companyId, "k", new UpdateCacheRequest { Value = "v" });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteCache_ReturnsOk_WhenDeleted()
        {
            var companyId = Guid.NewGuid();
            var controller = new CacheController(_service.Object);
            _service.Setup(s => s.DeleteAsync(companyId, null, "k")).ReturnsAsync(true);

            var result = await controller.DeleteCache(companyId, "k", null);
            Assert.IsType<OkObjectResult>(result);
        }
    }
}


// End of document
Prompt 2:
openapi: 3.0.3
info:
  title: Redis Cache Utility API
  version: 1.0.0
  description: RESTful API specification for managing cache data using Redis, scoped by company and office identifiers.
servers:
  - url: https://api.mycompany.com
    description: Production server
  - url: https://staging.api.mycompany.com
    description: Staging server

paths:
  /api/companies/{companyId}/cache:
    get:
      summary: Get a cache item by key
      operationId: getCache
      parameters:
        - name: companyId
          in: path
          required: true
          schema:
            type: string
            format: uuid
        - name: officeNumber
          in: query
          required: false
          schema:
            type: string
        - name: key
          in: query
          required: true
          schema:
            type: string
      responses:
        '200':
          description: Cache item retrieved successfully
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CacheItem'
              example:
                key: user:123
                value: '{"name":"John Doe"}'
                ttlSeconds: 3600
                officeNumber: '001'
        '400':
          description: Invalid input
        '404':
          description: Cache item not found
        '500':
          description: Internal server error

    post:
      summary: Set a new cache item
      operationId: setCache
      parameters:
        - name: companyId
          in: path
          required: true
          schema:
            type: string
            format: uuid
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/SetCacheRequest'
      responses:
        '200':
          description: Cache item created successfully
          content:
            application/json:
              schema:
                type: object
                properties:
                  success:
                    type: boolean
                example:
                  success: true
        '400':
          description: Invalid request body
        '500':
          description: Internal server error

  /api/companies/{companyId}/cache/{key}:
    put:
      summary: Update an existing cache item
      operationId: updateCache
      parameters:
        - name: companyId
          in: path
          required: true
          schema:
            type: string
            format: uuid
        - name: key
          in: path
          required: true
          schema:
            type: string
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdateCacheRequest'
      responses:
        '200':
          description: Cache item updated successfully
          content:
            application/json:
              schema:
                type: object
                properties:
                  success:
                    type: boolean
                example:
                  success: true
        '400':
          description: Invalid input
        '404':
          description: Cache item not found
        '500':
          description: Internal server error

    delete:
      summary: Delete a cache item by key
      operationId: deleteCache
      parameters:
        - name: companyId
          in: path
          required: true
          schema:
            type: string
            format: uuid
        - name: key
          in: path
          required: true
          schema:
            type: string
        - name: officeNumber
          in: query
          required: false
          schema:
            type: string
      responses:
        '200':
          description: Cache item deleted successfully
          content:
            application/json:
              schema:
                type: object
                properties:
                  success:
                    type: boolean
                example:
                  success: true
        '404':
          description: Cache item not found
        '500':
          description: Internal server error

  /api/companies/{companyId}/cache/invalidate:
    post:
      summary: Invalidate cache items based on a pattern
      operationId: invalidateCache
      parameters:
        - name: companyId
          in: path
          required: true
          schema:
            type: string
            format: uuid
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/InvalidateCacheRequest'
      responses:
        '200':
          description: Cache invalidation successful
          content:
            application/json:
              schema:
                type: object
                properties:
                  success:
                    type: boolean
                example:
                  success: true
        '400':
          description: Invalid input
        '500':
          description: Internal server error

components:
  schemas:
    CacheItem:
      type: object
      properties:
        key:
          type: string
        value:
          type: string
        ttlSeconds:
          type: integer
          format: int64
        officeNumber:
          type: string

    SetCacheRequest:
      type: object
      required:
        - key
        - value
      properties:
        key:
          type: string
          example: user:123
        value:
          type: string
          example: '{"name":"John Doe"}'
        ttlSeconds:
          type: integer
          format: int32
          example: 3600
        officeNumber:
          type: string
          example: '001'

    UpdateCacheRequest:
      type: object
      required:
        - value
      properties:
        value:
          type: string
          example: '{"name":"Updated User"}'
        ttlSeconds:
          type: integer
          format: int32
          example: 1800
        officeNumber:
          type: string
          example: '002'

    InvalidateCacheRequest:
      type: object
      properties:
        pattern:
          type: string
          example: 'user:*'
        officeNumber:
          type: string
          example: '001'

  responses:
    BadRequest:
      description: Invalid input or missing parameters
    NotFound:
      description: Resource not found
    InternalError:
      description: Internal server error
Prompt 3:
// =========================
// File: DTOs/ApiResponse.cs
// =========================
using System.Net;

namespace MyCompany.CacheApi.DTOs
{
    public class ApiResponse<T>
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> Success(T data, string message = "Success", HttpStatusCode status = HttpStatusCode.OK)
            => new ApiResponse<T> { StatusCode = status, Message = message, Data = data };

        public static ApiResponse<T> Fail(string message, HttpStatusCode status = HttpStatusCode.BadRequest)
            => new ApiResponse<T> { StatusCode = status, Message = message, Data = default };
    }
}


// =========================
// File: DTOs/GetCacheRequest.cs
// =========================
using System;
using System.ComponentModel.DataAnnotations;

namespace MyCompany.CacheApi.DTOs
{
    public class GetCacheRequest
    {
        [Required]
        public Guid CompanyId { get; set; }

        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Key { get; set; } = string.Empty;

        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/SetCacheRequest.cs
// =========================
using System.ComponentModel.DataAnnotations;

namespace MyCompany.CacheApi.DTOs
{
    public class SetCacheRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "TTL must be greater than zero.")]
        public int? TtlSeconds { get; set; }

        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/UpdateCacheRequest.cs
// =========================
using System.ComponentModel.DataAnnotations;

namespace MyCompany.CacheApi.DTOs
{
    public class UpdateCacheRequest
    {
        [Required]
        public string Value { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "TTL must be greater than zero.")]
        public int? TtlSeconds { get; set; }

        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/DeleteCacheRequest.cs
// =========================
using System;
using System.ComponentModel.DataAnnotations;

namespace MyCompany.CacheApi.DTOs
{
    public class DeleteCacheRequest
    {
        [Required]
        public Guid CompanyId { get; set; }

        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Key { get; set; } = string.Empty;

        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/InvalidateCacheRequest.cs
// =========================
using System;

namespace MyCompany.CacheApi.DTOs
{
    public class InvalidateCacheRequest
    {
        public Guid CompanyId { get; set; }

        public string? Pattern { get; set; }

        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/CacheItemResponse.cs
// =========================
namespace MyCompany.CacheApi.DTOs
{
    public class CacheItemResponse
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public long? TtlSeconds { get; set; }
        public string? OfficeNumber { get; set; }
    }
}


// =========================
// File: DTOs/InvalidateCacheResponse.cs
// =========================
namespace MyCompany.CacheApi.DTOs
{
    public class InvalidateCacheResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}


// =========================
// File: DTOs/DeleteCacheResponse.cs
// =========================
namespace MyCompany.CacheApi.DTOs
{
    public class DeleteCacheResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}


// =========================
// File: DTOs/UpdateCacheResponse.cs
// =========================
namespace MyCompany.CacheApi.DTOs
{
    public class UpdateCacheResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}


// =========================
// File: DTOs/SetCacheResponse.cs
// =========================
namespace MyCompany.CacheApi.DTOs
{
    public class SetCacheResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
Prompt 4:
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace RedisUtility.Api.Services
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string companyId, string? officeNumber = null);
        Task<bool> SetAsync<T>(string companyId, string? officeNumber, T value, TimeSpan? ttl = null);
        Task<bool> UpdateAsync<T>(string companyId, string? officeNumber, T value);
        Task<bool> DeleteAsync(string companyId, string? officeNumber = null);
        Task<bool> InvalidateAsync(string companyId, string? officeNumber = null);
    }

    public class CacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<CacheService> _logger;
        private readonly IConfiguration _config;

        private readonly IDatabase _db;
        private readonly string _keyPattern;

        public CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger, IConfiguration config)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _db = _redis.GetDatabase();
            _keyPattern = _config.GetValue<string>("Redis:KeyPattern") ?? "company:{0}:office:{1}";
        }

        private string BuildKey(string companyId, string? officeNumber)
        {
            return string.Format(_keyPattern, companyId, officeNumber ?? "default");
        }

        public async Task<T?> GetAsync<T>(string companyId, string? officeNumber = null)
        {
            try
            {
                var key = BuildKey(companyId, officeNumber);
                var value = await _db.StringGetAsync(key);

                if (value.IsNullOrEmpty)
                    return default;

                return JsonSerializer.Deserialize<T>(value!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cache for CompanyId={CompanyId}, OfficeNumber={OfficeNumber}", companyId, officeNumber);
                throw;
            }
        }

        public async Task<bool> SetAsync<T>(string companyId, string? officeNumber, T value, TimeSpan? ttl = null)
        {
            try
            {
                var key = BuildKey(companyId, officeNumber);
                var json = JsonSerializer.Serialize(value);

                return await _db.StringSetAsync(key, json, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for CompanyId={CompanyId}, OfficeNumber={OfficeNumber}", companyId, officeNumber);
                throw;
            }
        }

        public async Task<bool> UpdateAsync<T>(string companyId, string? officeNumber, T value)
        {
            try
            {
                var key = BuildKey(companyId, officeNumber);
                if (!await _db.KeyExistsAsync(key))
                    throw new InvalidOperationException($"Cache item not found for key {key}");

                var json = JsonSerializer.Serialize(value);
                return await _db.StringSetAsync(key, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cache for CompanyId={CompanyId}, OfficeNumber={OfficeNumber}", companyId, officeNumber);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string companyId, string? officeNumber = null)
        {
            try
            {
                var key = BuildKey(companyId, officeNumber);
                return await _db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting cache for CompanyId={CompanyId}, OfficeNumber={OfficeNumber}", companyId, officeNumber);
                throw;
            }
        }

        public async Task<bool> InvalidateAsync(string companyId, string? officeNumber = null)
        {
            try
            {
                var endpoints = _redis.GetEndPoints();
                var server = _redis.GetServer(endpoints[0]);

                string pattern = officeNumber == null
                    ? $"company:{companyId}:*"
                    : BuildKey(companyId, officeNumber);

                int deleted = 0;
                foreach (var key in server.Keys(pattern: pattern))
                {
                    if (await _db.KeyDeleteAsync(key))
                        deleted++;
                }

                _logger.LogInformation("Invalidated {Count} keys for CompanyId={CompanyId}, OfficeNumber={OfficeNumber}", deleted, companyId, officeNumber);
                return deleted > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for CompanyId={CompanyId}, OfficeNumber={OfficeNumber}", companyId, officeNumber);
                throw;
            }
        }
    }
}

Prompt 5:
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RedisUtility.Api.Models; // Namespace where ApiResponse<T> lives

namespace RedisUtility.Api.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception occurred during request processing.");

            var response = context.Response;
            response.ContentType = "application/json";

            HttpStatusCode statusCode = HttpStatusCode.InternalServerError;
            string message = "An unexpected error occurred.";

            // Handle known exception types
            switch (exception)
            {
                case ArgumentNullException:
                case ArgumentException:
                case InvalidOperationException:
                    statusCode = HttpStatusCode.BadRequest;
                    message = exception.Message;
                    break;
                default:
                    // Optionally include inner exception details in development mode
                    message = exception.Message;
                    break;
            }

            response.StatusCode = (int)statusCode;

            var errorResponse = new ApiResponse<string>
            {
                StatusCode = (int)statusCode,
                Message = message,
                Data = null
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await response.WriteAsync(json);
        }
    }

    // Extension method for registration
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}

using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RedisUtility.Api.Models; // Where ApiResponse<T> is defined

namespace RedisUtility.Api.Filters
{
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .Select(kvp => new
                    {
                        Field = kvp.Key,
                        Errors = kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    })
                    .ToArray();

                var response = new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = "Validation failed",
                    Data = errors
                };

                context.Result = new BadRequestObjectResult(response);
            }
        }
    }
}
Registration: Program.cs
using RedisUtility.Api.Middleware;
using RedisUtility.Api.Filters;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidateModelAttribute>();
});

var app = builder.Build();

// Global exception handling
app.UseGlobalExceptionHandling();

app.MapControllers();
app.Run();

Prompt 6:
{
  "info": {
    "name": "Redis Utility API",
    "_postman_id": "4b51b85a-ef88-44f8-a019-redis-api-utility",
    "description": "Postman collection for Redis Cache Utility API endpoints with example requests and responses.",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Get Cache",
      "request": {
        "method": "GET",
        "header": [
          {
            "key": "Authorization",
            "value": "Bearer {{token}}",
            "type": "text"
          }
        ],
        "url": {
          "raw": "{{baseUrl}}/api/cache?companyId={{companyId}}&officeNumber={{officeNumber}}",
          "host": ["{{baseUrl}}"],
          "path": ["api", "cache"],
          "query": [
            { "key": "companyId", "value": "{{companyId}}" },
            { "key": "officeNumber", "value": "{{officeNumber}}" }
          ]
        }
      },
      "response": [
        {
          "name": "200 OK - Success",
          "status": "OK",
          "code": 200,
          "body": "{\n  \"statusCode\": 200,\n  \"message\": \"Cache retrieved successfully.\",\n  \"data\": {\n    \"key\": \"company:123:office:45\",\n    \"value\": { \"someData\": \"example\" }\n  }\n}"
        },
        {
          "name": "404 Not Found - Missing Entry",
          "status": "Not Found",
          "code": 404,
          "body": "{\n  \"statusCode\": 404,\n  \"message\": \"Cache item not found.\",\n  \"data\": null\n}"
        }
      ]
    },
    {
      "name": "Set Cache",
      "request": {
        "method": "POST",
        "header": [
          {
            "key": "Authorization",
            "value": "Bearer {{token}}",
            "type": "text"
          },
          { "key": "Content-Type", "value": "application/json" }
        ],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"companyId\": \"123\",\n  \"officeNumber\": \"45\",\n  \"value\": { \"someData\": \"example\" },\n  \"ttlSeconds\": 3600\n}"
        },
        "url": {
          "raw": "{{baseUrl}}/api/cache",
          "host": ["{{baseUrl}}"],
          "path": ["api", "cache"]
        }
      },
      "response": [
        {
          "name": "200 OK - Cache Set",
          "status": "OK",
          "code": 200,
          "body": "{\n  \"statusCode\": 200,\n  \"message\": \"Cache set successfully.\",\n  \"data\": true\n}"
        },
        {
          "name": "400 Bad Request - Missing Company ID",
          "status": "Bad Request",
          "code": 400,
          "body": "{\n  \"statusCode\": 400,\n  \"message\": \"Validation failed\",\n  \"data\": [{ \"field\": \"companyId\", \"errors\": [\"CompanyId is required.\"] }]\n}"
        }
      ]
    },
    {
      "name": "Update Cache",
      "request": {
        "method": "PUT",
        "header": [
          {
            "key": "Authorization",
            "value": "Bearer {{token}}",
            "type": "text"
          },
          { "key": "Content-Type", "value": "application/json" }
        ],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"companyId\": \"123\",\n  \"officeNumber\": \"45\",\n  \"value\": { \"someData\": \"updated\" }\n}"
        },
        "url": {
          "raw": "{{baseUrl}}/api/cache",
          "host": ["{{baseUrl}}"],
          "path": ["api", "cache"]
        }
      },
      "response": [
        {
          "name": "200 OK - Updated",
          "status": "OK",
          "code": 200,
          "body": "{\n  \"statusCode\": 200,\n  \"message\": \"Cache updated successfully.\",\n  \"data\": true\n}"
        },
        {
          "name": "400 Bad Request - Not Found",
          "status": "Bad Request",
          "code": 400,
          "body": "{\n  \"statusCode\": 400,\n  \"message\": \"Cache item not found for given key.\",\n  \"data\": null\n}"
        }
      ]
    },
    {
      "name": "Delete Cache",
      "request": {
        "method": "DELETE",
        "header": [
          {
            "key": "Authorization",
            "value": "Bearer {{token}}",
            "type": "text"
          }
        ],
        "url": {
          "raw": "{{baseUrl}}/api/cache?companyId={{companyId}}&officeNumber={{officeNumber}}",
          "host": ["{{baseUrl}}"],
          "path": ["api", "cache"],
          "query": [
            { "key": "companyId", "value": "{{companyId}}" },
            { "key": "officeNumber", "value": "{{officeNumber}}" }
          ]
        }
      },
      "response": [
        {
          "name": "200 OK - Deleted",
          "status": "OK",
          "code": 200,
          "body": "{\n  \"statusCode\": 200,\n  \"message\": \"Cache deleted successfully.\",\n  \"data\": true\n}"
        }
      ]
    },
    {
      "name": "Invalidate Cache",
      "request": {
        "method": "POST",
        "header": [
          {
            "key": "Authorization",
            "value": "Bearer {{token}}",
            "type": "text"
          },
          { "key": "Content-Type", "value": "application/json" }
        ],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"companyId\": \"123\",\n  \"officeNumber\": null\n}"
        },
        "url": {
          "raw": "{{baseUrl}}/api/cache/invalidate",
          "host": ["{{baseUrl}}"],
          "path": ["api", "cache", "invalidate"]
        }
      },
      "response": [
        {
          "name": "200 OK - Cache Invalidated",
          "status": "OK",
          "code": 200,
          "body": "{\n  \"statusCode\": 200,\n  \"message\": \"Cache invalidated successfully.\",\n  \"data\": true\n}"
        },
        {
          "name": "400 Bad Request - Validation Failed",
          "status": "Bad Request",
          "code": 400,
          "body": "{\n  \"statusCode\": 400,\n  \"message\": \"Validation failed\",\n  \"data\": [{ \"field\": \"companyId\", \"errors\": [\"CompanyId is required.\"] }]\n}"
        }
      ]
    }
  ],
  "variable": [
    { "key": "baseUrl", "value": "http://localhost:5000" },
    { "key": "companyId", "value": "123" },
    { "key": "officeNumber", "value": "45" },
    { "key": "token", "value": "<your_token_here>" }
  ]
}
Prompt 7:
### ============================================================
### 🟢 GET CACHE - Positive Case
### Expected: 200 OK
### ============================================================
GET http://localhost:5000/api/cache?companyId=123&officeNumber=45
Authorization: Bearer {{token}}
Accept: application/json

### ============================================================
### 🔴 GET CACHE - Missing CompanyId (Negative)
### Expected: 400 Bad Request (Validation failed)
### ============================================================
GET http://localhost:5000/api/cache?officeNumber=45
Authorization: Bearer {{token}}
Accept: application/json


### ============================================================
### 🟢 SET CACHE - Positive Case
### Expected: 200 OK
### ============================================================
POST http://localhost:5000/api/cache
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "companyId": "123",
  "officeNumber": "45",
  "value": {
    "customerName": "John Doe",
    "balance": 250.75
  },
  "ttlSeconds": 3600
}

### ============================================================
### 🔴 SET CACHE - Missing CompanyId (Negative)
### Expected: 400 Bad Request
### ============================================================
POST http://localhost:5000/api/cache
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "officeNumber": "45",
  "value": {
    "customerName": "John Doe"
  }
}

### ============================================================
### 🔴 SET CACHE - Invalid JSON (Negative)
### Expected: 400 Bad Request / 500 Internal Server Error
### ============================================================
POST http://localhost:5000/api/cache
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "companyId": "123",
  "officeNumber": "45",
  "value": { "customerName": "Missing closing brace"
### malformed JSON


### ============================================================
### 🟢 UPDATE CACHE - Positive Case
### Expected: 200 OK
### ============================================================
PUT http://localhost:5000/api/cache
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "companyId": "123",
  "officeNumber": "45",
  "value": {
    "customerName": "John Doe",
    "balance": 300.00
  }
}

### ============================================================
### 🔴 UPDATE CACHE - Nonexistent Key (Negative)
### Expected: 400 Bad Request ("Cache item not found for given key.")
### ============================================================
PUT http://localhost:5000/api/cache
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "companyId": "999",
  "officeNumber": "99",
  "value": {
    "customerName": "Ghost",
    "balance": 0
  }
}


### ============================================================
### 🟢 DELETE CACHE - Positive Case
### Expected: 200 OK
### ============================================================
DELETE http://localhost:5000/api/cache?companyId=123&officeNumber=45
Authorization: Bearer {{token}}
Accept: application/json


### ============================================================
### 🔴 DELETE CACHE - Missing CompanyId (Negative)
### Expected: 400 Bad Request
### ============================================================
DELETE http://localhost:5000/api/cache?officeNumber=45
Authorization: Bearer {{token}}
Accept: application/json


### ============================================================
### 🟢 INVALIDATE CACHE - Positive Case
### Expected: 200 OK
### ============================================================
POST http://localhost:5000/api/cache/invalidate
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "companyId": "123",
  "officeNumber": null
}

### ============================================================
### 🔴 INVALIDATE CACHE - Missing CompanyId (Negative)
### Expected: 400 Bad Request (Validation failed)
### ============================================================
POST http://localhost:5000/api/cache/invalidate
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "officeNumber": "45"
}
Prompt 8:
Here’s a clean, professional `README.md` tailored for your **.NET 8 Redis Utility API** project — concise yet complete enough for new developers or DevOps teammates to quickly understand, configure, and test it.

---

```markdown
# 🚀 Redis Utility API (ASP.NET Core 8.0)

A lightweight, structured API for interacting with Redis cache using **.NET 8**, designed with clean architecture principles, async/await, and robust error handling.

---

## 🧠 Overview

This API provides endpoints to **get, set, update, delete, and invalidate** cache entries stored in Redis.  
Each cache entry is scoped by:
- `companyId` (required)
- `officeNumber` (optional)

The Redis key format is configurable (default: `company:{companyId}:office:{officeNumber}`).

---

## ⚙️ Features

- RESTful endpoints following clean architecture
- Centralized error handling middleware
- Request validation with model filters
- Strongly typed DTOs with data annotations
- Optional TTL (time-to-live) support for cache items
- Fully async implementation using `StackExchange.Redis`

---

## 🏗️ Project Structure

```

RedisUtility.Api/
│
├── Controllers/
│   └── CacheController.cs
│
├── Services/
│   └── CacheService.cs
│
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
│
├── Filters/
│   └── ValidateModelAttribute.cs
│
├── Models/
│   ├── Requests/
│   │   ├── GetCacheRequest.cs
│   │   ├── SetCacheRequest.cs
│   │   ├── UpdateCacheRequest.cs
│   │   ├── DeleteCacheRequest.cs
│   │   └── InvalidateCacheRequest.cs
│   └── Responses/
│       └── ApiResponse.cs
│
├── Tests/
│   └── CacheServiceTests.cs
│
├── api-test.http                # Manual API test file (VSCode/JetBrains compatible)
├── RedisUtility.Api.postman.json # Postman collection
└── README.md

````

---

## ⚡ Getting Started

### 1️⃣ Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Redis Server](https://redis.io/download) (local or remote)
- (Optional) [Docker](https://www.docker.com/) for containerized Redis

---

### 2️⃣ Clone & Build

```bash
git clone https://github.com/your-org/redis-utility-api.git
cd redis-utility-api
dotnet restore
dotnet build
````

---

### 3️⃣ Configure Redis Connection

Update your `appsettings.json`:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "KeyPattern": "company:{0}:office:{1}"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

Alternatively, use environment variables:

```bash
export Redis__ConnectionString="localhost:6379"
export Redis__KeyPattern="company:{0}:office:{1}"
```

---

### 4️⃣ Run the API

```bash
dotnet run --project RedisUtility.Api
```

The API will be available at:

```
http://localhost:5000
```

Swagger UI (OpenAPI) will be available at:

```
http://localhost:5000/swagger
```

---

## 🧪 Testing the API

### Option 1: Use the `.http` test file

Run requests directly inside VS Code or Rider:

```
api-test.http
```

### Option 2: Import the Postman Collection

Import `RedisUtility.Api.postman.json` into Postman.

### Option 3: Use `curl` examples

#### ✅ Set Cache

```bash
curl -X POST http://localhost:5000/api/cache \
-H "Content-Type: application/json" \
-H "Authorization: Bearer <token>" \
-d '{"companyId": "123", "officeNumber": "45", "value": {"key": "value"}, "ttlSeconds": 3600}'
```

#### ✅ Get Cache

```bash
curl -X GET "http://localhost:5000/api/cache?companyId=123&officeNumber=45" \
-H "Authorization: Bearer <token>"
```

#### ✅ Update Cache

```bash
curl -X PUT http://localhost:5000/api/cache \
-H "Content-Type: application/json" \
-d '{"companyId": "123", "officeNumber": "45", "value": {"key": "updatedValue"}}'
```

#### ✅ Delete Cache

```bash
curl -X DELETE "http://localhost:5000/api/cache?companyId=123&officeNumber=45"
```

#### ✅ Invalidate Cache

```bash
curl -X POST http://localhost:5000/api/cache/invalidate \
-H "Content-Type: application/json" \
-d '{"companyId": "123"}'
```

---

## 🧰 Error Handling

All responses conform to the unified format:

```json
{
  "statusCode": 400,
  "message": "Validation failed",
  "data": [
    {
      "field": "companyId",
      "errors": ["CompanyId is required."]
    }
  ]
}
```

---

## 🧪 Unit Tests

Run automated tests using:

```bash
dotnet test
```

---

## 📄 License

MIT License © 2025 Your Name / Company

```

---

Would you like me to include a **Dockerfile + docker-compose.yml** next so you can spin up Redis and the API together for local testing?
```
