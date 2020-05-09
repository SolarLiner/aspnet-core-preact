using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.SpaServices.Preact
{
    public static class PreactDevelopmentServerExtensions
    {
        public static void UsePreactDevelopmentServer(
            this ISpaBuilder builder, string script)
        {
            if(builder ==null) throw new ArgumentNullException(nameof(builder));
            SpaOptions options = builder.Options;
            if (string.IsNullOrEmpty(options.SourcePath))
            {
                throw new InvalidOperationException($"To use {nameof(UsePreactDevelopmentServer)}, you must supply a non-empty value for the {nameof(SpaOptions.SourcePath)} property of {nameof(SpaOptions)} when calling {nameof(SpaApplicationBuilderExtensions.UseSpa)}.");
            }

            if (string.IsNullOrEmpty(script))
                throw new InvalidOperationException(
                    $"You must specify a valid npm script name to start the development server");

            PreactDevelopmentServerMiddleware.Attach(builder, script);
        }

        public static void UsePreactDevelopmentServer(this ISpaBuilder builder) =>
            UsePreactDevelopmentServer(builder, "dev");
    }

    internal static class PreactDevelopmentServerMiddleware
    {
        private const string LogCategoryName = "Microsoft.AspNetCore.SpaServices.Preact";
        private static TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Builder and script are expected to be non-null, as parameters are expected to be checked by the calling function.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="script"></param>
        public static void Attach(ISpaBuilder builder, string script)
        {
            string sourcePath = builder.Options.SourcePath;

            IApplicationBuilder appBuilder = builder.ApplicationBuilder;
            ILogger logger = GetOrCreateLogger(appBuilder, LogCategoryName);
            Task<int> portTask = StartPreactServerAsync(sourcePath, script, logger);

            Task<Uri> targetUriTask = portTask.ContinueWith(task => new UriBuilder("http", "localhost", task.Result).Uri);
            SpaProxyingExtensions.UseProxyToSpaDevelopmentServer(builder, () =>
            {
                TimeSpan timeout = builder.Options.StartupTimeout;
                return targetUriTask.WithTimeout(timeout,
                    $"The preact development server didn't start within {timeout.Seconds} s.\nCheck the log output for " +
                    $"more information");
            });
        }

        private static async Task<int> StartPreactServerAsync(string sourcePath, string script, ILogger logger)
        {
            int port = FindAvailablePort();
            logger.LogInformation($"Starting preact dev server on port {port}");
            var runner = new NpmScriptRunner(sourcePath,
                script,
                null,
                new Dictionary<string, string>
                {
                    {
                        "PORT", port.ToString()
                    }
                });
            runner.AttachToLogger(logger);
            using var stderrReader = new EventedStreamStringReader(runner.StdErr);
            try
            {
                await runner.StdOut.WaitForMatch(new Regex("Compiled successfully!",
                    RegexOptions.None,
                    RegexMatchTimeout));
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidOperationException($"The script '{script}' exited without indicating it had " +
                                                    $"started successfully. stderr has indicated the following:\n"
                                                    +stderrReader.ReadAsString(), ex);
            }

            return port;
        }

        internal static ILogger GetOrCreateLogger(
            IApplicationBuilder appBuilder,
            string logCategoryName)
        {
            // If the DI system gives us a logger, use it. Otherwise, set up a default one
            var loggerFactory = appBuilder.ApplicationServices.GetService<ILoggerFactory>();
            ILogger logger = loggerFactory != null
                ? loggerFactory.CreateLogger(logCategoryName)
                : NullLogger.Instance;
            return logger;
        }

        public static int FindAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}