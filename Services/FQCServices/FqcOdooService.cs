using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;

namespace ScanCheckSakura.Services
{
    public interface IFqcOdooService
    {
        Task PostCommentBySerialAsync(string serial, string commentBody);
    }

    public class FqcOdooService : IFqcOdooService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<FqcOdooService> _logger;

        private const string BaseUrl              = "https://sigmaworldwide.io/web/dataset/call_kw";
        private const string StockLotUrl          = $"{BaseUrl}/stock.lot/web_search_read";
        private const string StockTraceabilityUrl = $"{BaseUrl}/stock.traceability.report/get_html";
        private const string PostCommentUrl       = "https://sigmaworldwide.io/mail/message/post";

        public FqcOdooService(HttpClient httpClient, ApplicationDbContext db, ILogger<FqcOdooService> logger)
        {
            _httpClient = httpClient;
            _db         = db;
            _logger     = logger;
        }

        // ── Lấy cookie từ DB ──────────────────────────────
        private async Task<string?> GetCookieAsync()
        {
            var record = await _db.SM_Defect_Cookie.FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(record?.cookie) ? null : record.cookie;
        }

        // ── Main flow: serial → lotId → woPageId → post comment ──
        public async Task PostCommentBySerialAsync(string serial, string commentBody)
        {
            // ── Cookie ────────────────────────────────────
            var cookie = await GetCookieAsync();
            if (cookie == null)
            {
                return;
            }
  

            var lotPayload = new
            {
                id = 18, jsonrpc = "2.0", method = "call",
                @params = new
                {
                    model = "stock.lot", method = "web_search_read",
                    args  = Array.Empty<object>(),
                    kwargs = new
                    {
                        limit = 1, offset = 0, order = "",
                        domain = new object[] { new object[] { "name", "ilike", serial } },
                        fields = new[] { "name", "product_id" }
                    }
                }
            };

            int lotId;
            using (var req1 = new HttpRequestMessage(HttpMethod.Post, StockLotUrl)
                   { Content = JsonContent.Create(lotPayload) })
            {
                req1.Headers.Add("Cookie", cookie);
                var res1 = await _httpClient.SendAsync(req1);

                res1.EnsureSuccessStatusCode();

                var raw1 = await res1.Content.ReadAsStringAsync();
             

                using var doc1 = JsonDocument.Parse(raw1);
                if (!doc1.RootElement.TryGetProperty("result", out var r)
                    || !r.TryGetProperty("records", out var records)
                    || records.ValueKind != JsonValueKind.Array
                    || records.GetArrayLength() == 0)
                {
                    return;
                }

                lotId = records[0].GetProperty("id").GetInt32();
               
            }

            // ── Step 2: lotId → woPageId (traceability) ──
          

            var tracePayload = new
            {
                id = 23, jsonrpc = "2.0", method = "call",
                @params = new
                {
                    args = new object[]
                    {
                        new
                        {
                            lang = "vi_VN", tz = "Asia/Ho_Chi_Minh",
                            uid  = 2, allowed_company_ids = new[] { 1 },
                            active_id = lotId, model = "stock.lot",
                            ttype = (object?)null, auto_unfold = false, lot_name = (object?)null
                        }
                    },
                    model = "stock.traceability.report", method = "get_html",
                    kwargs = new
                    {
                        context = new
                        {
                            lang = "vi_VN", tz = "Asia/Ho_Chi_Minh",
                            uid  = 2, allowed_company_ids = new[] { 1 }
                        }
                    }
                }
            };

            int woPageId;
            using (var req2 = new HttpRequestMessage(HttpMethod.Post, StockTraceabilityUrl)
                   { Content = JsonContent.Create(tracePayload) })
            {
                req2.Headers.Add("Cookie", cookie);
                var res2 = await _httpClient.SendAsync(req2);

                res2.EnsureSuccessStatusCode();

                using var doc2 = await JsonDocument.ParseAsync(await res2.Content.ReadAsStreamAsync());
                var html = doc2.RootElement
                    .GetProperty("result").GetProperty("html").GetString() ?? "";

               

                // Thử regex gốc
                var match = Regex.Match(html,
                    @"data-active-id=""(\d+)""[^>]*?>\s*(NM/MO/\d+(?:-\d+)?)\s*<",
                    RegexOptions.IgnoreCase);

             

                if (!match.Success)
                {
                    // Thử regex rộng hơn để xem format thực tế
                    var matchWide = Regex.Match(html, @"data-active-id=""(\d+)""", RegexOptions.IgnoreCase);
                 

                    var matchMO = Regex.Match(html, @"NM/MO/[\w\-]+", RegexOptions.IgnoreCase);
                   

                    return;
                }

                woPageId = int.Parse(match.Groups[1].Value);
                var workOrder = match.Groups[2].Value;
            }

            // ── Step 3: post comment ──────────────────────
    

            var commentPayload = new
            {
                id = 13, jsonrpc = "2.0", method = "call",
                @params = new
                {
                    post_data = new
                    {
                        attachment_ids      = Array.Empty<int>(),
                        attachment_tokens   = Array.Empty<string>(),
                        body                = commentBody,
                        message_type        = "comment",
                        partner_ids         = Array.Empty<int>(),
                        canned_response_ids = Array.Empty<int>(),
                        subtype_xmlid       = "mail.mt_comment"
                    },
                    thread_id    = woPageId,
                    thread_model = "mrp.production",
                    context = new
                    {
                        mail_post_autofollow = true,
                        lang = "vi_VN", tz = "Asia/Ho_Chi_Minh",
                        uid  = 2, allowed_company_ids = new[] { 1 }
                    }
                }
            };

            using var req3 = new HttpRequestMessage(HttpMethod.Post, PostCommentUrl)
                { Content = JsonContent.Create(commentPayload) };
            req3.Headers.Add("Cookie", cookie);

            var res3 = await _httpClient.SendAsync(req3);
            var body3 = await res3.Content.ReadAsStringAsync();

     
            if (!res3.IsSuccessStatusCode)
            {
              

            }
            else
            {
               
            }
        }
    }
}