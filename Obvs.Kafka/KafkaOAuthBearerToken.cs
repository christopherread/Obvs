using System;
using System.Collections.Generic;

namespace Obvs.Kafka
{
    /// <summary>A token for Kafka's <c>OAUTHBEARER</c> sign-in, returned by <see cref="KafkaOptions.OAuthBearerTokenProvider"/>.</summary>
    public sealed class KafkaOAuthBearerToken
    {
        public KafkaOAuthBearerToken(string value, DateTimeOffset expiresAt)
        {
            Value = value;
            ExpiresAt = expiresAt;
        }

        /// <summary>The token, for example a JWT from Microsoft Entra ID, or a signed token from AWS's MSK IAM signer.</summary>
        public string Value { get; }

        /// <summary>When the token expires. Kafka clients ask for a new one before then.</summary>
        public DateTimeOffset ExpiresAt { get; }

        /// <summary>The principal the token is for. Brokers read it from the token, so this is only for the client's logs; <c>obvs</c> when not set.</summary>
        public string PrincipalName { get; set; }

        /// <summary>SASL extensions to send with the token, for example <c>logicalCluster</c> and <c>identityPoolId</c> for Confluent Cloud.</summary>
        public IDictionary<string, string> Extensions { get; set; }
    }
}
