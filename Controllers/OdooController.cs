using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using Newtonsoft.Json.Linq;



[ApiController]
[Route("api/[controller]")]
public class OdooController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OdooController> _logger;

    private const string OdooApiUrl = "https://sigmaworldwide.io/web/dataset/call_kw/mrp.production/web_search_read";
    private const string BaseUrl = "https://sigmaworldwide.io/web/dataset/call_kw";
    private const string MrpProductionUrl = $"{BaseUrl}/mrp.production/web_search_read";
    private const string OdooPostCommentUrl = "https://sigmaworldwide.io/mail/message/post";
    
    private const string StockLotUrl = $"{BaseUrl}/stock.lot/web_search_read";
    private const string StockTraceabilityUrl = $"{BaseUrl}/stock.traceability.report/get_html";

    public OdooController(HttpClient httpClient, ApplicationDbContext db, ILogger<OdooController> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _logger = logger;
    }

    // ── Helper: lấy cookie từ DB ────────────────────────────────────────
    private async Task<string?> GetCookieFromDbAsync()
    {
        var record = await _db.SM_Defect_Cookie.FirstOrDefaultAsync();
        if (record == null || string.IsNullOrWhiteSpace(record.cookie))
        {
            Console.WriteLine("Cookie not found.");
            return null;
        }
        return record.cookie;
    }

    private async Task<(string? cookie, IActionResult? error)> RequireCookieAsync()
    {
        var cookie = await GetCookieFromDbAsync();
        if (cookie == null)
            return (null, StatusCode(500, new { message = "Odoo cookie not configured. Please update SM_Defect_Cookie table." }));
        return (cookie, null);
    }

    // ── Helper: build payload comment ──────────────────────────────────
    private static object BuildCommentPayload(string body, int threadId) => new
    {
        id = 13,
        jsonrpc = "2.0",
        method = "call",
        @params = new
        {
            post_data = new
            {
                attachment_ids = Array.Empty<int>(),
                attachment_tokens = Array.Empty<string>(),
                body,
                message_type = "comment",
                partner_ids = Array.Empty<int>(),
                canned_response_ids = Array.Empty<int>(),
                subtype_xmlid = "mail.mt_comment"
            },
            thread_id = threadId,
            thread_model = "mrp.production",
            context = new
            {
                mail_post_autofollow = true,
                lang = "vi_VN",
                tz = "Asia/Ho_Chi_Minh",
                uid = 2,
                allowed_company_ids = new[] { 1 }
            }
        }
    };

    // =====================================================================
    // GET /api/odoo/search
    // =====================================================================
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string productionCode)
    {
        var cookie = await GetCookieFromDbAsync();
        if (cookie == null)
            return StatusCode(500, new { message = "Odoo cookie not configured. Please update SVN_Defect_Cookie table." });

        string finalJson = $@"
            {{
                ""id"": 555555555,
                ""jsonrpc"": ""2.0"",
                ""method"": ""call"",
                ""params"": {{
                    ""model"": ""mrp.production"",
                    ""method"": ""web_search_read"",
                    ""args"": [],
                    ""kwargs"": {{
                        ""limit"": 1,
                        ""offset"": 0,
                        ""order"": ""name asc"",
                        ""context"": {{
                            ""lang"": ""vi_VN"",
                            ""tz"": ""Asia/Ho_Chi_Minh"",
                            ""uid"": 2,
                            ""allowed_company_ids"": [1],
                            ""bin_size"": true,
                            ""default_company_id"": 1
                        }},
                        ""count_limit"": 10001,
                        ""domain"": [
                            ""|"",
                              [""name"", ""like"", ""{productionCode}""],
                            [""origin"", ""like"", ""{productionCode}""]
                        ],
                        ""fields"": [""name"", ""origin"", ""product_id""]
                    }}
                }}
            }}";

        var jsonContent = new StringContent(finalJson, Encoding.UTF8, "application/json");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OdooApiUrl)
            {
                Content = jsonContent
            };
            request.Headers.Add("Cookie", cookie);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();

            if (responseBody.Contains("Session expired") || responseBody.Contains("SessionExpiredException"))
                return StatusCode(401, new { message = "Odoo session expired. Please update cookie in SM_Defect_Cookie table." });

            var productColor = ExtractProductColor(responseBody);
            if (!string.IsNullOrEmpty(productColor))
                return Ok(new { productColor });
            else
                return NotFound(new { message = "Product color not found" });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(500, new { message = "Error calling Odoo API", details = ex.Message });
        }
    }

    private string? ExtractProductColor(string jsonResponse)


    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (!root.TryGetProperty("result", out var result)) return null;
            if (!result.TryGetProperty("records", out var records)) return null;
            if (records.ValueKind != JsonValueKind.Array || records.GetArrayLength() == 0) return null;

            var firstRecord = records[0];
            if (!firstRecord.TryGetProperty("product_id", out var productId)) return null;
            if (productId.ValueKind != JsonValueKind.Array || productId.GetArrayLength() < 2) return null;

            var productDescription = productId[1].GetString();
            if (string.IsNullOrEmpty(productDescription)) return null;

            var match = Regex.Match(productDescription, @"\b(Blue|Green|Pink)\b", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting product color: {ex.Message}");
            return null;
        }
    }
    
    // Extract Produt Code
     private string ExtractProductCode(string jsonResponse)
    {
        try
        {
            var jsonObj = JObject.Parse(jsonResponse);
            var records = jsonObj["result"]?["records"] as JArray;

            if (records != null && records.Count > 0)
            {
                var firstRecord = records[0];
                var productId = firstRecord["product_id"] as JArray;

                if (productId != null && productId.Count >= 2)
                {
                    var productDescription = productId[1]?.ToString();
                    if (!string.IsNullOrEmpty(productDescription))
                    {
                        var match = Regex.Match(productDescription, @"^\[([^\]]+)\]");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting product code: {ex.Message}");
            return null;
        }
    }


    // =====================================================================
    // POST /api/odoo/postcomment
    // =====================================================================
    [HttpPost("postcomment")]
    public async Task<IActionResult> PostComment([FromBody] OdooCommentRequest req)
    {
        if (req == null || req.ThreadId <= 0 || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { ok = false, message = "Thiếu dữ liệu (threadId hoặc body)." });

        var (cookie, err) = await RequireCookieAsync();
        if (err != null) return err;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OdooPostCommentUrl)
            {
                Content = JsonContent.Create(BuildCommentPayload(req.Body, req.ThreadId!.Value))
            };
            request.Headers.Add("Cookie", cookie);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { ok = false, message = "Odoo API error", details = content });

            return Ok(new { ok = true, message = "Đã gửi comment thành công.", response = content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi post comment");
            return StatusCode(500, new { ok = false, message = "Lỗi khi gọi mail/message/post", details = ex.Message });
        }
    }

    // =====================================================================
    // POST /api/odoo/postcommenttoWO
    // =====================================================================
    [HttpPost("postcommenttoWO")]
    public async Task<IActionResult> PostCommentToWO([FromBody] OdooCommentRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.WOcode) || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { ok = false, message = "Thiếu dữ liệu (WOcode hoặc body)." });

        var (cookie, err) = await RequireCookieAsync();
        if (err != null) return err;

        var lookupPayload = new
        {
            id = 11,
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                model = "mrp.production",
                method = "web_search_read",
                args = Array.Empty<object>(),
                kwargs = new
                {
                    limit = 80,
                    offset = 0,
                    order = "",
                    context = new
                    {
                        lang = "vi_VN",
                        tz = "Asia/Ho_Chi_Minh",
                        uid = 2,
                        allowed_company_ids = new[] { 1 },
                        bin_size = true,
                        default_company_id = 1
                    },
                    count_limit = 10001,
                    domain = new object[]
                    {
                        "&",
                        new object[] { "picking_type_id.active", "=", true },
                        new object[] { "name", "ilike", req.WOcode }
                    },
                    fields = new[] { "id", "name" }
                }
            }
        };

        try
        {
            using var req1 = new HttpRequestMessage(HttpMethod.Post, MrpProductionUrl)
            {
                Content = JsonContent.Create(lookupPayload)
            };
            req1.Headers.Add("Cookie", cookie);

            var res1 = await _httpClient.SendAsync(req1);
            res1.EnsureSuccessStatusCode();

            using var doc1 = await JsonDocument.ParseAsync(await res1.Content.ReadAsStreamAsync());

            if (!doc1.RootElement.TryGetProperty("result", out var r)
                || !r.TryGetProperty("records", out var records)
                || records.ValueKind != JsonValueKind.Array
                || records.GetArrayLength() == 0)
                return NotFound(new { ok = false, message = $"Không tìm thấy Work Order: {req.WOcode}" });

            var threadId = records[0].GetProperty("id").GetInt32();

            using var request = new HttpRequestMessage(HttpMethod.Post, OdooPostCommentUrl)
            {
                Content = JsonContent.Create(BuildCommentPayload(req.Body, threadId))
            };
            request.Headers.Add("Cookie", cookie);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, new { ok = false, message = "Odoo API error", details = content });

            return Ok(new { ok = true, message = "Đã gửi comment thành công.", response = content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi post comment to WO");
            return StatusCode(500, new { ok = false, message = "Lỗi khi gọi Odoo", details = ex.Message });
        }
    }

    // =====================================================================
    // GET Work Order by Serial Number
    // GET /api/odoo/getworkorderfromserial?serial=xxx
    // =====================================================================
    [HttpGet("getworkorderfromserial")]
    public async Task<IActionResult> GetWorkOrderFromSerial([FromQuery] string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = "serial không được rỗng." });

        var (cookie, err) = await RequireCookieAsync();
        if (err != null) return err;

        // === Step 1: serial → lotId ===
        var payload1 = new
        {
            id = 18,
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                model = "stock.lot",
                method = "web_search_read",
                args = Array.Empty<object>(),
                kwargs = new
                {
                    limit = 80,
                    offset = 0,
                    order = "",
                    domain = new object[] { new object[] { "name", "ilike", serial } },
                    fields = new[] { "name", "product_id" }
                }
            }
        };

        int lotId;
        try
        {
            using var req1 = new HttpRequestMessage(HttpMethod.Post, StockLotUrl)
            {
                Content = JsonContent.Create(payload1)
            };
            req1.Headers.Add("Cookie", cookie);

            var res1 = await _httpClient.SendAsync(req1);
            res1.EnsureSuccessStatusCode();

            using var doc1 = await JsonDocument.ParseAsync(await res1.Content.ReadAsStreamAsync());

            if (!doc1.RootElement.TryGetProperty("result", out var r)
                || !r.TryGetProperty("records", out var records)
                || records.ValueKind != JsonValueKind.Array
                || records.GetArrayLength() == 0)
                return NotFound(new { message = "Không tìm thấy serial trong stock.lot." });

            lotId = records[0].GetProperty("id").GetInt32();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi stock.lot");
            return StatusCode(500, new { message = "Lỗi khi gọi stock.lot/web_search_read", details = ex.Message });
        }

        // === Step 2: lotId → workOrder (traceability) ===
        var payload2 = new
        {
            id = 23,
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                args = new object[]
                {
                new
                {
                    lang = "vi_VN",
                    tz = "Asia/Ho_Chi_Minh",
                    uid = 2,
                    allowed_company_ids = new[] { 1 },
                    active_id = lotId,
                    model = "stock.lot",
                    ttype = (object?)null,
                    auto_unfold = false,
                    lot_name = (object?)null
                }
                },
                model = "stock.traceability.report",
                method = "get_html",
                kwargs = new
                {
                    context = new
                    {
                        lang = "vi_VN",
                        tz = "Asia/Ho_Chi_Minh",
                        uid = 2,
                        allowed_company_ids = new[] { 1 }
                    }
                }
            }
        };

        try
        {
            using var req2 = new HttpRequestMessage(HttpMethod.Post, StockTraceabilityUrl)
            {
                Content = JsonContent.Create(payload2)
            };
            req2.Headers.Add("Cookie", cookie);

            var res2 = await _httpClient.SendAsync(req2);
            res2.EnsureSuccessStatusCode();

            using var doc2 = await JsonDocument.ParseAsync(await res2.Content.ReadAsStreamAsync());
            var html = doc2.RootElement
                .GetProperty("result")
                .GetProperty("html")
                .GetString() ?? "";

            var match = Regex.Match(html,
                @"data-active-id=""(\d+)""[^>]*?>\s*(NM/MO/\d+(?:-\d+)?)\s*<",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return NotFound(new { message = "Không tìm thấy Work Order trong traceability report." });

            var woPageId = int.Parse(match.Groups[1].Value);
            var workOrder = match.Groups[2].Value;
            var baseWorkOrder = Regex.Replace(workOrder, @"-\d+$", "");

            return Ok(new { serial, workOrder = baseWorkOrder, woPageId, serialPageId = lotId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi stock.traceability.report");
            return StatusCode(500, new { message = "Lỗi khi gọi stock.traceability.report/get_html", details = ex.Message });
        }
    }

    // =====================================================================
    // GET /api/odoo/getproductfromserial?serial=xxx
    // Get Product from Serial Number
    // =====================================================================

    

   
    [HttpGet("getproductfromserial")]
    public async Task<IActionResult> GetProductFromSerial([FromQuery] string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = "serial không được rỗng." });

        var (cookie, err) = await RequireCookieAsync();
        if (err != null) return err;

        // Query thẳng mrp.production theo lot_name (Lot/Serial Number field)
        var payload = new
        {
            id = 19,
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                model = "mrp.production",
                method = "web_search_read",
                args = Array.Empty<object>(),
                kwargs = new
                {
                    limit = 1,
                    offset = 0,
                    order = "name asc",
                    context = new
                    {
                        lang = "vi_VN",
                        tz = "Asia/Ho_Chi_Minh",
                        uid = 2,
                        allowed_company_ids = new[] { 1 },
                        bin_size = true,
                        default_company_id = 1
                    },
                    count_limit = 10001,
                    domain = new object[]
                    {
                    new object[] { "lot_producing_id.name", "=", serial }  
                    },
                    fields = new[] { "name", "product_id", "lot_producing_id" }
                }
            }
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, MrpProductionUrl)
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("Cookie", cookie);

            var res = await _httpClient.SendAsync(req);
            res.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());

            if (!doc.RootElement.TryGetProperty("result", out var result)
                || !result.TryGetProperty("records", out var records)
                || records.ValueKind != JsonValueKind.Array
                || records.GetArrayLength() == 0)
                return NotFound(new { message = $"Không tìm thấy MO nào có Lot/Serial = {serial}" });

            var record = records[0];
            var woName = record.GetProperty("name").GetString();
            var productId = record.GetProperty("product_id");
            var productIdNum = productId[0].GetInt32();
            var productName = productId[1].GetString();

            return Ok(new
            {
                serial,
                workOrder = woName,           // "NM/MO/02414-001"
                productId = productIdNum,
                productName                     // "Sakura Folio Case, Midnight Blue, RM15A-1000NW"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi getproductfromserial");
            return StatusCode(500, new { message = "Lỗi", details = ex.Message });
        }
    }





    // =====================================================================
    // GET /api/odoo/getmessagefromserial?serial=xxx&hashTag=@FQC
    // =====================================================================
    [HttpGet("getmessagefromserial")]
    public async Task<IActionResult> GetMessageFromSerial(
        [FromQuery] string serial,
        [FromQuery] string hashTag)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = "serial không được rỗng." });
        if (string.IsNullOrWhiteSpace(hashTag))
            return BadRequest(new { message = "hashTag không được rỗng." });

        var (cookie, err) = await RequireCookieAsync();
        if (err != null) return err;

        // === Step 1: serial → lotId ===
        var payload1 = new
        {
            id = 18,
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                model = "stock.lot",
                method = "web_search_read",
                args = Array.Empty<object>(),
                kwargs = new
                {
                    limit = 80,
                    offset = 0,
                    order = "",
                    domain = new object[] { new object[] { "name", "ilike", serial } },
                    fields = new[] { "name", "product_id" }
                }
            }
        };

        int lotId;
        try
        {
            using var req1 = new HttpRequestMessage(HttpMethod.Post, StockLotUrl)
            {
                Content = JsonContent.Create(payload1)
            };
            req1.Headers.Add("Cookie", cookie);

            var res1 = await _httpClient.SendAsync(req1);
            res1.EnsureSuccessStatusCode();

            using var doc1 = await JsonDocument.ParseAsync(await res1.Content.ReadAsStreamAsync());

            if (!doc1.RootElement.TryGetProperty("result", out var r)
                || !r.TryGetProperty("records", out var records)
                || records.ValueKind != JsonValueKind.Array
                || records.GetArrayLength() == 0)
                return NotFound(new { message = "Không tìm thấy serial trong stock.lot." });

            lotId = records[0].GetProperty("id").GetInt32();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi stock.lot");
            return StatusCode(500, new { message = "Lỗi khi gọi stock.lot/web_search_read", details = ex.Message });
        }

        // === Step 2: lotId → workOrder + woPageId (traceability) ===
        var payload2 = new
        {
            id = 23,
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                args = new object[]
                {
                    new
                    {
                        lang = "vi_VN",
                        tz = "Asia/Ho_Chi_Minh",
                        uid = 2,
                        allowed_company_ids = new[] { 1 },
                        active_id = lotId,
                        model = "stock.lot",
                        ttype = (object?)null,
                        auto_unfold = false,
                        lot_name = (object?)null
                    }
                },
                model = "stock.traceability.report",
                method = "get_html",
                kwargs = new
                {
                    context = new
                    {
                        lang = "vi_VN",
                        tz = "Asia/Ho_Chi_Minh",
                        uid = 2,
                        allowed_company_ids = new[] { 1 }
                    }
                }
            }
        };

        string? workOrder = null;
        int woPageId = 0;

        try
        {
            using var req2 = new HttpRequestMessage(HttpMethod.Post, StockTraceabilityUrl)
            {
                Content = JsonContent.Create(payload2)
            };
            req2.Headers.Add("Cookie", cookie);

            var res2 = await _httpClient.SendAsync(req2);
            res2.EnsureSuccessStatusCode();

            using var doc2 = await JsonDocument.ParseAsync(await res2.Content.ReadAsStreamAsync());
            var html = doc2.RootElement
                .GetProperty("result")
                .GetProperty("html")
                .GetString() ?? "";

            var match = Regex.Match(html,
                @"data-active-id=""(\d+)""[^>]*?>\s*(NM/MO/\d+(?:-\d+)?)\s*<",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return NotFound(new { message = "Không tìm thấy Work Order trong traceability report." });

            woPageId  = int.Parse(match.Groups[1].Value);
            workOrder = match.Groups[2].Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi stock.traceability.report");
            return StatusCode(500, new { message = "Lỗi khi gọi stock.traceability.report/get_html", details = ex.Message });
        }

        // === Step 3: woPageId → thread messages → lọc hashTag ===
        var payload3 = new
        {
            id = 9,
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                thread_id = woPageId,
                thread_model = "mrp.production",
                searchDomain = Array.Empty<object>(),
                limit = 30
            }
        };

        try
        {
            using var req3 = new HttpRequestMessage(HttpMethod.Post, "https://sigmaworldwide.io/mail/thread/messages")
            {
                Content = JsonContent.Create(payload3)
            };
            req3.Headers.Add("Cookie", cookie);

            var res3 = await _httpClient.SendAsync(req3);
            res3.EnsureSuccessStatusCode();

            using var doc3 = await JsonDocument.ParseAsync(await res3.Content.ReadAsStreamAsync());

            if (!doc3.RootElement.TryGetProperty("result", out var messages)
                || messages.ValueKind != JsonValueKind.Array)
                return NotFound(new { message = "Không lấy được messages từ thread." });

            var foundMessages = new List<object>();
            foreach (var msg in messages.EnumerateArray())
            {
                if (!msg.TryGetProperty("body", out var bodyElem)
                    || bodyElem.ValueKind != JsonValueKind.String)
                    continue;

                var body = bodyElem.GetString() ?? "";
                if (!body.Contains(hashTag, StringComparison.OrdinalIgnoreCase))
                    continue;

                var date = msg.TryGetProperty("date", out var dateElem)
                    && dateElem.ValueKind == JsonValueKind.String
                    ? dateElem.GetString()
                    : null;

                foundMessages.Add(new { message = body, date });
            }

            return Ok(new
            {
                serial,
                workOrder,
                woPageId,
                serialPageId = lotId,
                hashTag,
                found = foundMessages.Count > 0,
                messages = foundMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy thread messages");
            return StatusCode(500, new { message = "Lỗi khi gọi mail/thread/messages", details = ex.Message });
        }
    }

    // ── Request model ────────────────────────────────────────────────────
    public class OdooCommentRequest
    {
        public int? ThreadId { get; set; }
        public string Body { get; set; } = string.Empty;
        public string? WOcode { get; set; }
    }
}
