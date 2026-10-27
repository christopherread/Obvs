using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Hosting;

namespace Obvs.Testing
{
    /// <summary>
    /// Keeps a committed copy of a bus's contract (its AsyncAPI document) and fails on a breaking change, from a test:
    /// <code>ObvsContractSnapshot.Verify(harness.Services, "contracts/orders.asyncapi.json");</code>
    /// The first run writes the file, to commit. Later runs pass when the contract is the same, update the file when the
    /// contract only grows, and throw <see cref="ObvsContractException"/> on a breaking change, leaving the file as it was.
    /// </summary>
    public static class ObvsContractSnapshot
    {
        /// <param name="services">A container with the bus, such as a test harness's</param>
        /// <param name="path">The committed document, relative to the test's working directory or absolute</param>
        /// <param name="serviceName">One service to check, or all of the bus's services</param>
        public static void Verify(IServiceProvider services, string path, string serviceName = null)
        {
            var contracts = services.GetRequiredService<ObvsContracts>();
            var current = serviceName == null ? contracts.Describe() : contracts.Describe(serviceName);
            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, current);
                return;
            }

            var committed = File.ReadAllText(fullPath);
            if (committed == current)
            {
                return;
            }

            var breaking = ObvsContractComparer.FindBreakingChanges(committed, current);
            if (breaking.Count > 0)
            {
                throw new ObvsContractException(
                    $"The contract has changes that would break apps built against {fullPath}:" + Environment.NewLine +
                    string.Join(Environment.NewLine, breaking.Select(change => "- " + change)) + Environment.NewLine +
                    "If they're intended, and every app using these messages is updated with them, delete the file and run again to accept them.");
            }
            File.WriteAllText(fullPath, current);
        }
    }

    /// <summary>A bus's contract has changed in a way that would break apps built against its committed copy.</summary>
    public sealed class ObvsContractException : Exception
    {
        /// <summary>The breaking changes, described.</summary>
        /// <param name="message">Each change, and how to accept them</param>
        public ObvsContractException(string message) : base(message)
        {
        }
    }
}
