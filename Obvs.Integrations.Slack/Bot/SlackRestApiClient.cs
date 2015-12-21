using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Obvs.Integrations.Slack.Api;

namespace Obvs.Integrations.Slack.Bot
{
    internal interface ISlackRestApi
    {
        Task<TResult> Post<TResult>(string method, IEnumerable<KeyValuePair<string, string>> args = null);
    }

    internal class SlackRestApi : ISlackRestApi
    {
		readonly Uri _endpoint = new Uri("https://slack.com/api/");
		readonly HttpClient _http = new HttpClient();
		readonly string _token;

		public SlackRestApi(string token)
		{
			_token = token;

			// Make things faster.
			_http.DefaultRequestHeaders.ExpectContinue = false;
		}

		public async Task<TResult> Post<TResult>(string method, IEnumerable<KeyValuePair<string, string>> args = null)
		{
			// Append the auth token to the args (required for all requests).
			var postArgs =
				(args ?? Enumerable.Empty<KeyValuePair<string, string>>())
				.Union(new[] {
					new KeyValuePair<string, string>("token", _token)
				});

			var postData = new FormUrlEncodedContent(postArgs);
			
			Debug.WriteLine("REST SEND: " + await postData.ReadAsStringAsync());
			var httpResponse = await _http.PostAsync(new Uri(_endpoint, method), postData);

			// Stash the repsonse in a memory stream.
			var json = await httpResponse.Content.ReadAsStringAsync();
			Debug.WriteLine("REST RCV: " + json);

			// Create serialisers for our error type (to check if we're valid) and the specific type
			// we've been asked to deserialise into.
			var errorResponse = Serialiser.Deserialise<ErrorResponse>(json);

		    if (!errorResponse.OK)
		    {
		        throw new Exception(errorResponse.Error);
		    }

		    return Serialiser.Deserialise<TResult>(json);
		}
	}
}
