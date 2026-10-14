using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Launcher.Services;

namespace Ntools
{
    /// <summary>
    /// Provides utility methods for file operations.
    /// </summary>
    public static class Nfile
    {
        /// <summary>
        /// The maximum number of retries for downloading a file.
        /// </summary>
        private const int MaxRetries = 3;

        /// <summary>
        /// The HttpClient instance used for downloading files.
        /// </summary>
        private static readonly HttpClient HttpClient;

        /// <summary>
        /// The HttpMessageHandler instance used for handling HTTP requests.
        /// </summary>
        private static readonly HttpMessageHandler HttpMessageHandler;

        /// <summary>
        /// The timeout value for HTTP requests in milliseconds.
        /// </summary>
        private static int Timeout { get; set; } = 300000;  // 300000 = 5 minutes 
        private const string UserAgent = "sdo";

        /// <summary>
        /// The list of allowed file extensions.
        /// </summary>
        private static List<string> AllowedExtensions { get; set; } = new List<string>();

        /// <summary>
        /// The list of trusted hosts.
        /// </summary>
        private static List<string> TrustedHosts { get; set; } = new List<string>();

        static Nfile()
        {
            // Set up the HttpClient
            HttpMessageHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (errors == SslPolicyErrors.None)
                    {
                        return true;
                    }

                    ConsoleHelper.WriteWarning($"SSL certificate error: {errors}");
                    return false;
                }
            };

            HttpClient = new HttpClient(HttpMessageHandler);
            HttpClient.Timeout = TimeSpan.FromMilliseconds(Timeout);
            HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
        }

        /// <summary>
        /// Gets the list of trusted hosts.
        /// </summary>
        /// <returns>The list of trusted hosts.</returns>
        public static List<string> GetTrustedHosts()
        {
            return TrustedHosts;
        }

        /// <summary>
        /// Sets the list of trusted hosts.
        /// </summary>
        /// <param name="trustedHosts">The list of trusted hosts.</param>
        public static void SetTrustedHosts(List<string> trustedHosts)
        {
            TrustedHosts = trustedHosts;
        }

        /// <summary>
        /// Gets the list of allowed file extensions.
        /// </summary>
        /// <returns>The list of allowed file extensions.</returns>
        public static List<string> GetAllowedExtensions()
        {
            return AllowedExtensions;
        }

        /// <summary>
        /// Sets the list of allowed file extensions.
        /// </summary>
        /// <param name="allowedExtensions">The list of allowed file extensions.</param>
        public static void SetAllowedExtensions(List<string> allowedExtensions)
        {
            AllowedExtensions = allowedExtensions;
        }

        /// <summary>
        /// Downloads a file asynchronously from the specified URL.
        /// </summary>
        /// <param name="url">The URL of the file to download.</param>
        /// <param name="downloadedFilename">The name of the downloaded file.</param>
        /// <returns>A task representing the asynchronous download operation.</returns>
        public static async Task<ResultDownload> DownloadAsync(string url, string downloadedFilename)
        {
            var safeUri = CreateSafeUri(url);
            var resultDownload = new ResultDownload(safeUri, downloadedFilename);

            if (!ValidExtension(url.ToString())) throw new ArgumentException("Invalid uri extension", nameof(url));

            if (string.IsNullOrEmpty(downloadedFilename)) throw new ArgumentException("Invalid file name.", nameof(downloadedFilename));

            if (!Path.IsPathRooted(downloadedFilename)) throw new ArgumentException("Invalid file name: Path must be rooted.", nameof(downloadedFilename));

            // Check if downloadedFilename contains invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (Path.GetFileName(downloadedFilename).IndexOfAny(invalidChars) >= 0) throw new ArgumentException($"Invalid file name: downloadedFilename contains one or more invalid characters {invalidChars}.", nameof(downloadedFilename));

            int retryCount = 0;
            while (retryCount < MaxRetries)
            {
                try
                {
                    // Set file size in the return object.  This could be used to check if size is within a range at a later release
                    resultDownload.SetFileSize();
                    if (File.Exists(downloadedFilename)) File.Delete(downloadedFilename);

                    using (var s = await HttpClient.GetStreamAsync(url))
                    using (var fs = new FileStream(downloadedFilename, FileMode.Create))
                    {
                        await s.CopyToAsync(fs);
                    }
                    resultDownload.Success();
                }
                catch (OperationCanceledException ex)
                {
                    resultDownload.Fail($"Operation canceled: {ex.Message}");
                }
                catch (HttpRequestException ex)
                {
                    var status = ex.StatusCode.HasValue
                        ? $" HTTP {(int)ex.StatusCode.Value} ({ex.StatusCode.Value})."
                        : string.Empty;
                    resultDownload.Fail($"Download request failed on try # {retryCount}.{status} {ex.Message}");
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= 3)
                    {
                        resultDownload.Fail($"An exception occurred in download try # {retryCount}: {ex.Message}");
                    }

                    await Task.Delay(1000); // wait before retrying
                }
                finally
                {
                    //httpStream?.Dispose();
                    //fileStream?.Dispose();
                    retryCount = MaxRetries;
                }
            }

            if (resultDownload.IsSuccess())
            {
                // Check if file got downnloaed and do any cleanup here
                if (!File.Exists(downloadedFilename))
                {
                    resultDownload.Fail($"File {downloadedFilename} does not exist.");
                }
                else
                {
                    // get downloaded file size from the local file
                    var fileInfo = new FileInfo(downloadedFilename);
                    long fileSize = fileInfo.Length;

                    if (fileSize != resultDownload.FileSize) resultDownload.Fail($"File size mismatch. Expected: {resultDownload.FileSize}, Actual: {fileSize}.");

                    //Set digital signature and file size
                    resultDownload.SetFileSigned();

                    // and compare with the size from the server
                    resultDownload.SetFileSize();
                }
            }

            return resultDownload;
        }

        /// <summary>
        /// Checks if a URI exists asynchronously.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <returns>A task representing the asynchronous existence check.</returns>
        public static async Task<HttpResponseMessage> UriExistsAsync(string uri)
        {
            if (!ValidUri(uri)) throw new ArgumentException("Invalid uri", nameof(uri));

            try
            {
                return await HttpClient.GetAsync(uri);
            }
            catch (HttpRequestException)
            {
                // pass through exception to caller
                throw;
            }
        }

        /// <summary>
        /// Gets the file size of a URI asynchronously.
        /// </summary>
        /// <param name="uri">The URI to get the file size of.</param>
        /// <returns>A task representing the asynchronous file size retrieval.</returns>
        public static async Task<long> GetFileSizeAsync(string uri)
        {
            if (!ValidUri(uri.ToString())) throw new ArgumentException("Invalid uri", nameof(Uri));

            try
            {
                var response = await HttpClient.GetAsync(uri);
                return response.Content.Headers.ContentLength ?? 0;
            }
            catch (HttpRequestException)
            {
                // pass through exception to caller
                throw;
            }
        }

        /// <summary>
        /// Checks if a host is trusted.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <returns>True if the host is trusted; otherwise, false.</returns>
        public static bool TrustedHost(Uri uri)
        {
            foreach (var trustedHost in TrustedHosts)
            {
                if (uri.Host == trustedHost)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a URI is valid.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <returns>True if the URI is valid; otherwise, false.</returns>
        public static bool ValidUri(string uri)
        {
            var result = ValidUriInternal(uri, out Uri uriResult);

            // Disallow traversal paths
            if (result)
            {
                result = !uriResult.AbsolutePath.Contains("..");
            }

            return result;
        }

        private static bool ValidUriInternal(string uri, out Uri uriResult)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            return Uri.TryCreate(uri, UriKind.Absolute, out uriResult)
                        && uriResult.Scheme == Uri.UriSchemeHttps;
        }

        /// <summary>
        /// Checks if a URI has a valid extension.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <returns>True if the URI has a valid extension; otherwise, false.</returns>
        public static bool ValidExtension(string uri)
        {
            var result = ValidUriInternal(uri, out Uri uriResult);
            if (result)
            {
                string fileExtension = Path.GetExtension(uriResult.AbsolutePath);
                result = AllowedExtensions.Contains(fileExtension);
            }

            return result;
        }

        /// <summary>
        /// Creates a safe URI from the user input.
        /// </summary>
        /// <param name="userInput">The user input to create the URI from.</param>
        /// <returns>The safe URI.</returns>
        public static Uri CreateSafeUri(string userInput)
        {
            if (string.IsNullOrEmpty(userInput))
            {
                throw new ArgumentException("Input cannot be null or empty", nameof(userInput));
            }

            if (!Uri.TryCreate(userInput, UriKind.Absolute, out Uri uri))
            {
                throw new ArgumentException("Invalid URI", nameof(userInput));
            }

            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("Non-secure URI", nameof(userInput));
            }

            if (!TrustedHost(uri))
            {
                throw new ArgumentException("Untrusted host", nameof(userInput));
            }

            Uri baseUri = new Uri(uri.GetLeftPart(UriPartial.Path));
            if (!baseUri.IsBaseOf(uri))
            {
                throw new ArgumentException("Path traversal detected", nameof(userInput));
            }

            return uri;
        }

    }
}
