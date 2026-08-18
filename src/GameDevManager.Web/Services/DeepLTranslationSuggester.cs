using System.Text.Json;
using GameDevManager.Data;
using GameDevManager.Data.Services;
using Microsoft.Extensions.Localization;

namespace GameDevManager.Web.Services;

/// <summary>
/// Die DeepL-Konfiguration — <c>DeepL:*</c> in den appsettings (der Schlüssel gehört in die
/// <c>appsettings.Local.json</c> und steht in keinem Export). Ohne Schlüssel ist der
/// Vorschlags-Knopf im Raster gar nicht erst da.
/// </summary>
public sealed class DeepLOptions
{
    public const string SectionName = "DeepL";

    public string? ApiKey { get; set; }

    /// <summary>
    /// Der Endpunkt. Leer heißt: aus dem Schlüssel abgeleitet — Free-Schlüssel enden auf
    /// „:fx“ und gehören auf api-free.deepl.com, alle anderen auf api.deepl.com.
    /// </summary>
    public string? Endpoint { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public string ResolveEndpoint() =>
        !string.IsNullOrWhiteSpace(Endpoint)
            ? Endpoint.TrimEnd('/')
            : ApiKey!.TrimEnd().EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
                ? "https://api-free.deepl.com"
                : "https://api.deepl.com";
}

/// <summary>
/// DeepL als erster Anbieter hinter <see cref="ITranslationSuggester"/> — ein zweiter ist
/// nur eine weitere Klasse. Drei Dinge:
/// <list type="bullet">
/// <item>Erwähnungen und Platzhalter gehen <b>versiegelt</b> über die Leitung
/// (<see cref="TranslationText"/>, <c>tag_handling=xml</c> + <c>ignore_tags=x</c>) — sie
/// überleben den Vorschlag unverändert.</item>
/// <item>Die Sprachkürzel des Projekts werden über <see cref="DeepLLanguageMap"/> abgebildet,
/// mit Rückfall auf die Hauptsprache.</item>
/// <item>Fehler des Anbieters werden zu verständlichen Meldungen: Kontingent (456) und
/// Schlüssel (401/403) haben eigene Texte statt eines Statuscodes.</item>
/// </list>
/// </summary>
public sealed class DeepLTranslationSuggester(
    DeepLOptions options,
    IHttpClientFactory clients,
    IStringLocalizer<DataMessages> messages) : ITranslationSuggester
{
    public bool IsConfigured => options.IsConfigured;

    public string ProviderName => "DeepL";

    public async Task<string> SuggestAsync(
        string text, string? sourceLanguageCode, string targetLanguageCode, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var payload = new Dictionary<string, object?>
        {
            ["text"] = new[] { TranslationText.ToXml(text) },
            ["target_lang"] = DeepLLanguageMap.Map(targetLanguageCode, isTarget: true),
            ["tag_handling"] = "xml",
            ["ignore_tags"] = new[] { "x" }
        };

        if (!string.IsNullOrWhiteSpace(sourceLanguageCode))
        {
            payload["source_lang"] = DeepLLanguageMap.Map(sourceLanguageCode, isTarget: false);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{options.ResolveEndpoint()}/v2/translate")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"DeepL-Auth-Key {options.ApiKey}");

        HttpResponseMessage response;

        try
        {
            response = await clients.CreateClient(nameof(DeepLTranslationSuggester)).SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ContentValidationException(messages["TranslateProviderUnreachable", ex.Message].Value);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Die beiden Fälle, die im Alltag auftreten, bekommen eigene Worte — ein
                // Statuscode erklärte niemandem, dass das Kontingent des Monats leer ist.
                throw new ContentValidationException((int)response.StatusCode switch
                {
                    456 => messages["TranslateQuotaExceeded"].Value,
                    401 or 403 => messages["TranslateKeyRejected"].Value,
                    var status => messages["TranslateProviderError", status].Value
                });
            }

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            var translated = json.RootElement
                .GetProperty("translations")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            return TranslationText.FromXml(translated);
        }
    }
}
