namespace Da
{
    using Autodesk.Forge.Core;
    using Autodesk.Forge.DesignAutomation;
    using Microsoft.Extensions.CommandLineUtils;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="AzureStorageConfig" />.
    /// </summary>
    public class AzureStorageConfig
    {
        /// <summary>
        /// Gets or sets the StorageConnectionString.
        /// </summary>
        public string StorageConnectionString { get; set; }
    }

    /// <summary>
    /// Defines the <see cref="FilePaths" />.
    /// </summary>
    public static class FilePaths
    {
        /// <summary>
        /// Gets or sets the InputFile.
        /// </summary>
        public static string InputFile { get; set; }

        /// <summary>
        /// Gets or sets the OutPut.
        /// </summary>
        public static string OutPut { get; set; }
    }

    /// <summary>
    /// Defines the <see cref="ConsoleHost" />.
    /// </summary>
    class ConsoleHost : IHostedService
    {
        /// <summary>
        /// The StartAsync.
        /// </summary>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="Task"/>.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// The StopAsync.
        /// </summary>
        /// <param name="cancellationToken">The cancellationToken<see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="Task"/>.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Defines the <see cref="Program" />.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The Main.
        /// </summary>
        /// <param name="args">The args<see cref="string[]"/>.</param>
        /// <returns>The <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            var cli = new CommandLineApplication(throwOnUnexpectedArg: true)
            {
                FullName = "A CLI application to rapid test design automation workflows!",
                Description = "An utility app to process input drawings",
                ExtendedHelpText = "\nda -i <input.zip> -o <output folder>\n"

            };

            var helpOption = cli.HelpOption("-? | -h | --help");
            if (helpOption.HasValue())
            {
                cli.ShowHelp();
                cli.ShowRootCommandFullNameAndVersion();
                return;
            }
            if (args.Length == 0)
            {
                cli.ShowHelp();
                cli.ShowRootCommandFullNameAndVersion();
                return;
            }
            string version = typeof(Program).Assembly.GetName().Version.ToString();
            cli.VersionOption("-v", version, string.Format("version {0}", version));
            var input = cli.Option("-i", "Full path to the input zip file.", CommandOptionType.SingleValue);
            var output = cli.Option("-o", "Full path to the output folder.", CommandOptionType.SingleValue);
            cli.Execute(args);
            if (!input.HasValue() || !output.HasValue())
            {
                cli.ShowRootCommandFullNameAndVersion();
                return;
            }
            if (string.IsNullOrWhiteSpace(input.Values[0]) ||
                string.IsNullOrWhiteSpace(output.Values[0]))
            {
                cli.ShowRootCommandFullNameAndVersion();
                return;
            }
            FilePaths.InputFile = input.Values[0];
            if (output.Values[0].Equals("."))
            {
                FilePaths.OutPut = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
            else
                FilePaths.OutPut = output.Values[0];
            // Use HostBuilder to bootstrap the application
            var host = new HostBuilder()
                .ConfigureHostConfiguration(builder =>
                {
                    // some logging settings
                    builder.AddJsonFile("appsettings.json");
                })
                .ConfigureAppConfiguration(builder =>
                {
                    // TODO1: you must supply your appsettings.user.json with the following content:
                    //{
                    //    "Forge": {
                    //        "ClientId": "<your client Id>",
                    //        "ClientSecret": "<your secret>"
                    //    }
                    //}
                    builder.AddJsonFile("appsettings.user.json");
                    // Next line means that you can use Forge__ClientId and Forge__ClientSecret environment variables
                    builder.AddEnvironmentVariables();
                    // Finally, allow the use of "legacy" FORGE_CLIENT_ID and FORGE_CLIENT_SECRET environment variables
                    builder.AddForgeAlternativeEnvironmentVariables();
                })
                .ConfigureLogging((hostContext, builder) =>
                {
                    // set up console logging, could be skipped but useful
                    builder.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                    builder.AddConsole();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // add our no-op host (required by the HostBuilder)
                    services.AddHostedService<ConsoleHost>();

                    // our own app where all the real stuff happens
                    services.AddSingleton<App>();

                    // add and configure DESIGN AUTOMATION
                    services.AddDesignAutomation(hostContext.Configuration);
                    services.AddOptions();
                    services.Configure<AzureStorageConfig>(hostContext.Configuration.GetSection("AzureCloudStorage"));
                })
                .UseConsoleLifetime()
                .Build();
            using (host)
            {
                await host.StartAsync();

                // Get a reference to our App and run it
                var app = host.Services.GetRequiredService<App>();
                await app.RunAsync();

                await host.StopAsync();
            }
        }
    }
}
