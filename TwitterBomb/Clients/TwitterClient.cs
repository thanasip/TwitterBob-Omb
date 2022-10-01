using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using TwitterBobomb.Models;
using Newtonsoft.Json;

namespace TwitterBobomb.Clients
{
    public class TwitterClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiKeySecret;

        public delegate void TweetDeletedEventHandler(object sender, TweetDeletedEventArgs e);

        public event TweetDeletedEventHandler? TweetDeletedHandler;

        protected virtual void OnTweetDeleted(TweetDeletedEventArgs e)
        {
            TweetDeletedHandler?.Invoke(this, e);
        }

        public TwitterClient(string apiKey, string apiKeySecret)
        {
            _apiKey = apiKey;
            _apiKeySecret = apiKeySecret;
            _httpClient = new HttpClient();
        }

        public async Task<bool> DeleteTweet(string tweetId, string token, string tokenSecret)
        {
            var method = "DELETE";
            var baseUri = $"https://api.twitter.com/2/tweets/{tweetId}";
            var nonce = Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var oAuthSigParameters = new Dictionary<string, string>
            {
                {"oauth_consumer_key",$"{_apiKey}"},
                {"oauth_nonce",$"{nonce}"},
                {"oauth_signature_method","HMAC-SHA1"},
                {"oauth_timestamp",$"{timestamp}"},
                {"oauth_version","1.0"},
                {"oauth_token",$"{token}"}
            };

            var authParameters = CreateSignedAuthParameters(oAuthSigParameters, method, baseUri, tokenSecret);
            var responseString = await SendRequest(method, baseUri, authParameters);
            var result =  JsonConvert.DeserializeObject<TweetDeletedDto>(responseString)?.Data?.Deleted ?? false;
            if (result) OnTweetDeleted(new TweetDeletedEventArgs(tweetId));

            return result;
        }

        public async Task<string> GetLastFiveTweetsForUser(string userId, string token, string tokenSecret)
        {
            var method = "GET";
            var baseUri = $"https://api.twitter.com/2/users/{userId}/tweets";
            var nonce = Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var oAuthSigParameters = new Dictionary<string, string>
            {
                {"oauth_consumer_key",$"{_apiKey}"},
                {"oauth_nonce",$"{nonce}"},
                {"oauth_signature_method","HMAC-SHA1"},
                {"oauth_timestamp",$"{timestamp}"},
                {"oauth_version","1.0"},
                {"oauth_token",$"{token}"},
                {"max_results","5"}
            };

            var authParameters = CreateSignedAuthParameters(oAuthSigParameters, method, baseUri, tokenSecret);
            var responseString = await SendRequest(method, baseUri, authParameters, "max_results=5");
            return responseString;
        }

        public async Task<string> GetAuthedUser(string token, string tokenSecret)
        {
            var method = "GET";
            var baseUri = "https://api.twitter.com/2/users/me";
            var nonce = Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var oAuthSigParameters = new Dictionary<string, string>
            {
                {"oauth_consumer_key",$"{_apiKey}"},
                {"oauth_nonce",$"{nonce}"},
                {"oauth_signature_method","HMAC-SHA1"},
                {"oauth_timestamp",$"{timestamp}"},
                {"oauth_version","1.0"},
                {"oauth_token",$"{token}"}
            };

            var authParameters = CreateSignedAuthParameters(oAuthSigParameters, method, baseUri, tokenSecret);
            var responseString = await SendRequest(method, baseUri, authParameters);
            return responseString;
        }

        public async Task<string> GetRequestToken()
        {
            var method = "POST";
            var baseUri = "https://api.twitter.com/oauth/request_token";
            var nonce = Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var oAuthSigParameters = new Dictionary<string, string>
            {
                {"oauth_consumer_key",$"{_apiKey}"},
                {"oauth_nonce",$"{nonce}"},
                {"oauth_signature_method","HMAC-SHA1"},
                {"oauth_timestamp",$"{timestamp}"},
                {"oauth_version","1.0"},
                {"oauth_callback","oob"}
            };

            var authParameters = CreateSignedAuthParameters(oAuthSigParameters, method, baseUri);
            var responseString = await SendRequest(method, baseUri, authParameters, "oauth_callback=oob");
            return responseString.Split('&')[0].Split('=')[1];
        }

        public async Task<string> GetAccessToken(string oauthToken, string pinCode)
        {
            var uri = $"https://api.twitter.com/oauth/access_token?oauth_token={oauthToken}&oauth_verifier={pinCode}";
            var response = await _httpClient.PostAsync(uri, null);
            return await response.Content.ReadAsStringAsync();
        }

        private string CreateSignedAuthParameters(Dictionary<string,string> oAuthSigParameters, string method, string baseUri, string? tokenSecret = null)
        {
            var sig = SignRequest(oAuthSigParameters, method, baseUri, tokenSecret);

            var authParameters = new Dictionary<string, string>
            {
                {"oauth_consumer_key",$"{_apiKey}"},
                {"oauth_nonce",$"{oAuthSigParameters["oauth_nonce"]}"},
                {"oauth_signature_method","HMAC-SHA1"},
                {"oauth_signature",$"{sig}"},
                {"oauth_timestamp",$"{oAuthSigParameters["oauth_timestamp"]}"},
                {"oauth_version","1.0"},
            };

            if (oAuthSigParameters.Keys.Contains("oauth_token"))
            {
                authParameters.Add("oauth_token", oAuthSigParameters["oauth_token"]);
            }

            return CreateOAuthHeader(authParameters.OrderBy(kv => kv.Key).ToDictionary(k => k.Key, v => v.Value));
        }

        private string SignRequest(Dictionary<string,string> oAuthSigParameters, string method, string baseUri, string? tokenSecret = null)
        {
            var encParamKeys = oAuthSigParameters
                .Select(kv => new KeyValuePair<string, string>(WebUtility.UrlEncode(kv.Key), WebUtility.UrlEncode(kv.Value)))
                .OrderBy(kv => kv.Key)
                .ToDictionary(k => k.Key, v => v.Value);

            var index = 0;
            var paramString = new StringBuilder();
            foreach (var kv in encParamKeys)
            {
                index++;
                paramString.Append(kv.Key);
                paramString.Append("=");
                paramString.Append(kv.Value);
                paramString.Append(index < encParamKeys.Count ? "&" : "");
            }

            var sigBaseString = new StringBuilder();
            sigBaseString.Append(method);
            sigBaseString.Append("&");
            sigBaseString.Append(WebUtility.UrlEncode(baseUri));
            sigBaseString.Append("&");
            sigBaseString.Append(WebUtility.UrlEncode(paramString.ToString()));

            var signingKey = new StringBuilder();
            signingKey.Append(WebUtility.UrlEncode(_apiKeySecret));
            signingKey.Append("&");
            if (tokenSecret != null) signingKey.Append(WebUtility.UrlEncode(tokenSecret));

            return Convert.ToBase64String(new HMACSHA1(Encoding.ASCII.GetBytes(signingKey.ToString())).ComputeHash(Encoding.ASCII.GetBytes(sigBaseString.ToString())));
        }

        private string CreateOAuthHeader(Dictionary<string,string> authParameters)
        {
            var index = 0;
            var authHeaderVal = new StringBuilder();
            foreach (var kv in authParameters)
            {
                index++;
                authHeaderVal.Append(WebUtility.UrlEncode(kv.Key));
                authHeaderVal.Append("=");
                authHeaderVal.Append("\"");
                authHeaderVal.Append(WebUtility.UrlEncode(kv.Value));
                authHeaderVal.Append("\"");
                authHeaderVal.Append(index < authParameters.Count ? ", " : "");
            }

            return authHeaderVal.ToString();
        }

        private async Task<string> SendRequest(string method, string baseUri, string authParameters, string? queryString = null)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), $"{baseUri}{(queryString == null ? "" : "?")}{queryString}");
            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authParameters);
            var response = await _httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        } 

        private class TweetDeletedDto
        {
            public TweetDeleted? Data { get; set; }
        }

        private class TweetDeleted
        {
            public bool? Deleted { get; set; }
        }
    }
}
