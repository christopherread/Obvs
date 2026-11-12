using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Obvs.Kafka.Configuration
{
    // Runs KafkaOptions.OAuthBearerTokenProvider when a Kafka client asks for a token, handing the client the token or the
    // reason there isn't one. Clients ask on a thread of their own, so waiting for the provider there doesn't block the app.
    internal static class KafkaOAuthBearer
    {
        private const string DefaultPrincipalName = "obvs";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        public static void Refresh(Func<CancellationToken, Task<KafkaOAuthBearerToken>> provider,
            Action<string, long, string, IDictionary<string, string>> setToken, Action<string> setFailure)
        {
            KafkaOAuthBearerToken token;
            try
            {
                using (var cancellation = new CancellationTokenSource(Timeout))
                {
                    token = provider(cancellation.Token).GetAwaiter().GetResult();
                }
            }
            catch (Exception exception)
            {
                setFailure($"OAuthBearerTokenProvider failed: {exception.Message}");
                return;
            }

            if (string.IsNullOrEmpty(token?.Value))
            {
                setFailure("OAuthBearerTokenProvider returned no token.");
                return;
            }

            try
            {
                setToken(token.Value, token.ExpiresAt.ToUnixTimeMilliseconds(),
                    string.IsNullOrEmpty(token.PrincipalName) ? DefaultPrincipalName : token.PrincipalName, token.Extensions);
            }
            catch (Exception exception)
            {
                // For example a token that has already expired
                setFailure($"The token from OAuthBearerTokenProvider was refused: {exception.Message}");
            }
        }
    }
}
