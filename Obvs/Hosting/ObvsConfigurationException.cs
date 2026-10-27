using System;
using System.Collections.Generic;
using System.Linq;

namespace Obvs.Hosting
{
    /// <summary>
    /// Thrown when the bus starts with an invalid configuration. Lists every problem found, each with how to fix it.
    /// </summary>
    public sealed class ObvsConfigurationException : Exception
    {
        /// <summary>The configuration's problems, each with how to fix it.</summary>
        /// <param name="errors">Each problem found</param>
        public ObvsConfigurationException(IReadOnlyList<string> errors)
            : base("The Obvs service bus configuration is invalid:" + Environment.NewLine +
                   string.Join(Environment.NewLine, errors.Select(error => "- " + error)))
        {
            Errors = errors;
        }

        /// <summary>Each problem found, saying what's wrong and the API that fixes it.</summary>
        public IReadOnlyList<string> Errors { get; }
    }
}
