using Api.Contracts.Requests;
using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GeoController : ControllerBase
    {
        private readonly IGeoApplicationService _geoService;

        public GeoController(IGeoApplicationService geoService)
        {
            _geoService = geoService;
        }

        /// <summary>
        /// Single IP lookup
        /// GET /api/geo/{ip}
        /// </summary>
        [HttpGet("{ip}")]
        public async Task<IActionResult> GetGeoForIp(string ip)
        {
            var result = await _geoService.GetGeoForIpAsync(ip);

            return !result.IsSuccess ?
                MapErrorToResponse(result.Error) :
                Ok(result.Value);
        }

        /// <summary>
        /// Batch endpoint
        /// POST /api/geo/batch
        /// Body: ["8.8.8.8", "1.1.1.1", ...]
        /// Returns: BatchId + URL to check status
        /// </summary>
        [HttpPost("batch")]
        public async Task<IActionResult> EnqueueBatch([FromBody] BatchGeoRequest request)
        {
            if (request.Ips.Count == 0) return BadRequest(new { error = "At least one IP is required." });

            var result = await _geoService.EnqueueBatchAsync(request.Ips.ToArray());
            if (!result.IsSuccess) return MapErrorToResponse(result.Error);

            var dto = result.Value;

            dto.StatusUrl = Url.Action(
                nameof(GetBatchStatus),
                "Geo",
                new { id = dto.BatchId },
                Request.Scheme
            ) ?? string.Empty;

            return Accepted(dto);
        }

        /// <summary>
        /// Batch status endpoint
        /// GET /api/geo/batch/{id}
        /// </summary>
        [HttpGet("batch/{id:guid}")]
        public async Task<IActionResult> GetBatchStatus(Guid id)
        {
            var result = await _geoService.GetBatchStatusAsync(id);

            return result.IsSuccess ?
                Ok(result.Value) :
                MapErrorToResponse(result.Error);
        }

        /// <summary>
        /// Central mapping from domain/app errors to HTTP responses.
        /// </summary>
        private IActionResult MapErrorToResponse(Error error)
        {
            return error.Type switch
            {
                ErrorType.Validation =>
                    BadRequest(new { error = error.Message }),

                ErrorType.NotFound =>
                    NotFound(new { error = error.Message }),

                ErrorType.Conflict =>
                    Conflict(new { error = error.Message }),

                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        error = string.IsNullOrWhiteSpace(error.Message)
                            ? "Unexpected error"
                            : error.Message
                    })
            };
        }
    }
}
