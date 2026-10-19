using System.Text.Json;
using SPTushonka.Common.Http;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickSell.Patches
{
    /// <summary>
    /// Fetches the bulk flea price table from the QuickSell server mod.
    ///
    /// Deliberately a ONE-SHOT fetch at startup rather than a polling loop.
    ///
    /// On a Fika server every connected player runs this client. A 30-second poll would mean each
    /// player generating two requests a minute forever, multiplied by the player count, and some of
    /// those players are on other continents where that round trip is expensive. Fetching once per
    /// session means ten players cost ten requests total, all at menu time where latency is
    /// invisible. A manual refresh is available in the F12 menu for anyone who wants fresher
    /// numbers, which keeps the choice with the user instead of on a timer.
    ///
    /// Never called in raid, and never called on a headless client.
    /// </summary>
    internal static class FleaPriceClient
    {
        private const string Route = "/quicksell/getFleaPrices";

        private static bool _fetchInFlight;

        /// <summary>
        /// Requests the price table. Safe to call when the server mod is absent - it logs and the
        /// mod carries on with per-hover lookups instead.
        /// </summary>
        public static void FetchAsync(Action onComplete = null, bool forceServerRefresh = false)
        {
            if (_fetchInFlight) return;
            if (ModEnvironment.IsHeadlessClient) return;

            _fetchInFlight = true;

            Task.Run(() =>
            {
                try
                {
                    var route = forceServerRefresh ? Route + "?force=true" : Route;
                    // 5.0 的 RequestHandler 走 SPTushonka 加密响应，自定义 endpoint 返回的是明文，
                    // 会被 deshuffle 拒绝；这里直接用本机 HttpClient 取明文。
                    var baseUrl = (RequestHandler.Host ?? "https://127.0.0.1:6969").TrimEnd('/');
                    using var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };
                    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                    var json = http.GetStringAsync(baseUrl + route).GetAwaiter().GetResult();

                    if (string.IsNullOrEmpty(json))
                    {
                        Plugin.LogSource?.LogInfo(
                            "QuickSell: no flea price table returned. The server mod is probably not " +
                            "installed - flea prices will be looked up per item instead, and will not " +
                            "be available in raid.");
                        return;
                    }

                    var prices = new Dictionary<string, double>(StringComparer.Ordinal);
                    using (var doc = JsonDocument.Parse(json))
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.Value.TryGetDouble(out var value))
                                prices[prop.Name] = value;
                        }
                    }

                    if (prices == null || prices.Count == 0)
                    {
                        Plugin.LogSource?.LogWarning("QuickSell: flea price table was empty.");
                        return;
                    }

                    // LoadBulk swaps the price table and queues the aggregate rebuild. The
                    // finished tooltip cache is invalidated only after that rebuild completes on
                    // Unity's main thread, so refresh never exposes a half-updated state.
                    FleaPriceCache.LoadBulk(prices);

                    onComplete?.Invoke();
                }
                catch (Exception ex)
                {
                    Plugin.LogSource?.LogInfo(
                        $"QuickSell: could not fetch the flea price table ({ex.Message}). " +
                        "Falling back to per-item lookups outside raid.");
                }
                finally
                {
                    _fetchInFlight = false;
                }
            });
        }

        /// <summary>
        /// Re-fetches the table without clearing the currently usable prices first.
        ///
        /// Keeping the old snapshot alive during the request avoids a short window where tooltips
        /// lose their flea line and report incomplete prices. The new table replaces the old one
        /// only after the server response has arrived successfully.
        /// </summary>
        public static void ForceRefresh()
        {
            if (ModEnvironment.IsInRaid)
            {
                Utils.SendError("Flea prices cannot be refreshed during a raid");
                return;
            }

            FetchAsync(forceServerRefresh: true);
        }
    }
}
