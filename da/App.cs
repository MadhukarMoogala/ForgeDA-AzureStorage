namespace Da
{
    using Autodesk.Forge.Core;
    using Autodesk.Forge.DesignAutomation;
    using Autodesk.Forge.DesignAutomation.Model;
    using Microsoft.Azure.Storage;
    using Microsoft.Azure.Storage.Blob;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;

    /// <summary>
    /// Defines the <see cref="Extension" />.
    /// </summary>
    public static class Extension
    {
        /// <summary>
        /// The Mask.
        /// </summary>
        /// <param name="url">The url<see cref="string"/>.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public static string Mask(this string url)
        {
            int position = url.IndexOf('?');
            string restofString = url.Substring(position);
            return url.Replace(restofString, "/***Masked***");
        }
    }

    /// <summary>
    /// Defines the <see cref="App" />.
    /// </summary>
    class App
    {
        /// <summary>
        /// Defines the PackageName.
        /// </summary>
        static readonly string PackageName = "UpdateIPTParams";

        /// <summary>
        /// Defines the ActivityName.
        /// </summary>
        static readonly string ActivityName = "ActivityIptParam";

        /// <summary>
        /// Defines the Owner.
        /// </summary>
        static readonly string Owner = "dasdev";

        /// <summary>
        /// Defines the inputFileNameOSS.
        /// </summary>
        static string inputFileNameOSS = string.Format("{0}_input_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(FilePaths.InputFile));

        /// <summary>
        /// Defines the outputFileNameOSS.
        /// </summary>
        static string outputFileNameOSS = string.Format("{0}_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), "result.ipt");

        /// <summary>
        /// Defines the AzureContainer.
        /// </summary>
        static string AzureContainer = string.Format("{0}-{1}", "das", DateTime.Now.Ticks.ToString());

        /// <summary>
        /// Defines the UploadUrl.
        /// </summary>
        static string UploadUrl = "";

        /// <summary>
        /// Defines the DownloadUrl.
        /// </summary>
        static string DownloadUrl = "";

        /// <summary>
        /// Defines the Label.
        /// </summary>
        static readonly string Label = "prod";

        /// <summary>
        /// Defines the TargetEngine.
        /// </summary>
        static readonly string TargetEngine = "Autodesk.Inventor+2021";

        /// <summary>
        /// Defines the api.
        /// </summary>
        public DesignAutomationClient api;

        /// <summary>
        /// Defines the config.
        /// </summary>
        public ForgeConfiguration config;

        /// <summary>
        /// Defines the storageConfig.
        /// </summary>
        public AzureStorageConfig storageConfig;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        /// <param name="api">The api<see cref="DesignAutomationClient"/>.</param>
        /// <param name="config">The config<see cref="IOptions{ForgeConfiguration}"/>.</param>
        /// <param name="storageConfig">The storageConfig<see cref="IOptions{AzureStorageConfig}"/>.</param>
        public App(DesignAutomationClient api, IOptions<ForgeConfiguration> config, IOptions<AzureStorageConfig> storageConfig)
        {
            this.api = api;
            this.config = config.Value;
            this.storageConfig = storageConfig.Value;
        }

        /// <summary>
        /// The RunAsync.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        public async Task RunAsync()
        {
            if (string.IsNullOrEmpty(Owner))
            {
                Console.WriteLine("Please provide non-empty Owner.");
                return;
            }

            if (string.IsNullOrEmpty(UploadUrl))
            {
                //. Upload input file and get signed URL
                Console.WriteLine($"\tUploading Input file IPT file to Azure");
                try
                {

                    //Start of Azure
                    //download, & upload are w.r.t to Design Automation server
                    //        i--                 --o
                    //    {Down}|--->[IO]---->| {Up}

                    Console.WriteLine("Started uploading: " + FilePaths.InputFile);
                    DownloadUrl = await GetAzureUrlAsync(UrlType.downLoadUrl, inputFileNameOSS, FilePaths.InputFile);
                    UploadUrl = await GetAzureUrlAsync(UrlType.upLoadUrl, outputFileNameOSS);



                }
                catch (Exception ex)
                {
                    Console.WriteLine($"!!Error!! ->\n\t{ex.StackTrace}");
                    throw;
                }


            }

            if (!await SetupOwnerAsync())
            {
                Console.WriteLine("Exiting.");
                return;
            }
            //We don't need app bundle for this app
            var myApp = await SetupAppBundleAsync();
            var myActivity = await SetupActivityAsync(myApp);

            await SubmitWorkItemAsync(myActivity);
        }

        /// <summary>
        /// The SubmitWorkItemAsync.
        /// </summary>
        /// <param name="myActivity">The myActivity<see cref="string"/>.</param>
        /// <returns>The <see cref="Task"/>.</returns>
        private async Task SubmitWorkItemAsync(string myActivity)
        {
            Console.WriteLine("Submitting up workitem...");
            dynamic inputJson = new JObject();
            inputJson.Width = "40.0";
            inputJson.Height = "80.0";
            var workItemStatus = await api.CreateWorkItemAsync(new Autodesk.Forge.DesignAutomation.Model.WorkItem()
            {
                ActivityId = myActivity,
                Arguments = new Dictionary<string, IArgument>() {
                              {
                              "inputFile",
                                new XrefTreeArgument(){
                                Url = DownloadUrl,
                                Verb = Verb.Get
                               }
                              },
                              {
                              "inputJson",
                                new XrefTreeArgument(){
                                 Url = "data:application/json, " + ((JObject)inputJson).ToString(Formatting.None).Replace("\"", "'")
                                }
                              },
                              {
                              "outputFile",
                                new XrefTreeArgument(){
                                Verb = Verb.Put,
                                Url = UploadUrl,
                                Headers = new Dictionary<string, string>()
                                {
                                    { "Content-Type","application/octet-stream" },
                                    { "x-ms-blob-type","BlockBlob" }
                                }
                               }
                              }
                             }
            });

            Console.Write("\tPolling status");
            while (!workItemStatus.Status.IsDone())
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                workItemStatus = await api.GetWorkitemStatusAsync(workItemStatus.Id);
                Console.Write(".");
            }
            Console.WriteLine($"{workItemStatus.Status}.");
            var fname = await DownloadToDocsAsync(workItemStatus.ReportUrl, "Das-report.txt");
            if (workItemStatus.Status != Status.Success)
            {
                Console.WriteLine($"{workItemStatus.Status} Please refer log {fname} further details.. exiting! ");
                return;
            }
            Console.WriteLine($"Downloaded {fname}.");
            var urlString = await GetAzureUrlAsync(UrlType.downLoadUrl, outputFileNameOSS);
            var result = await DownloadToDocsAsync(urlString, outputFileNameOSS);
            Console.WriteLine($"Downloaded {result}.");
        }

        /// <summary>
        /// The SetupActivityAsync.
        /// </summary>
        /// <param name="myApp">The myApp<see cref="string"/>.</param>
        /// <returns>The <see cref="Task{string}"/>.</returns>
        private async Task<string> SetupActivityAsync(string myApp)
        {
            Console.WriteLine("Setting up activity...");
            var myActivity = $"{Owner}.{ActivityName}+{Label}";
            var actResponse = await this.api.ActivitiesApi.GetActivityAsync(myActivity, throwOnError: false);
            var activity = new Activity()
            {
                Appbundles = new List<string>()
                    {
                        myApp
                    },
                CommandLine = new List<string>()
                    {
                        $"$(engine.path)\\InventorCoreConsole.exe /i $(args[inputFile].path) /al $(appbundles[{PackageName}].path) $(args[inputJson].path)"
                    },
                Settings = new Dictionary<string, ISetting>()
                    {
                        { "script", new StringSetting(){ Value = string.Empty } }
                    },
                Engine = TargetEngine,
                Parameters = new Dictionary<string, Parameter>()
                    {
                        { "inputFile", new Parameter() { Verb= Verb.Get, LocalName = "$(inputFile)",  Required = true}},
                        { "inputJson", new Parameter() { Verb =Verb.Get, LocalName = "params.json",  Required = true}},
                        { "outputFile", new Parameter() { Verb= Verb.Put, LocalName = "outputFile.ipt", Required= true}}
                    },
                Id = ActivityName
            };
            if (actResponse.HttpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Creating activity {myActivity}...");
                await api.CreateActivityAsync(activity, Label);
                return myActivity;
            }
            await actResponse.HttpResponse.EnsureSuccessStatusCodeAsync();
            Console.WriteLine("\tFound existing activity...");
            if (!Equals(activity, actResponse.Content))
            {
                Console.WriteLine($"\tUpdating activity {myActivity}...");
                await api.UpdateActivityAsync(activity, Label);
            }
            return myActivity;

            static bool Equals(Autodesk.Forge.DesignAutomation.Model.Activity a, Autodesk.Forge.DesignAutomation.Model.Activity b)
            {
                Console.Write("\tComparing activities...");
                //ignore id and version
                b.Id = a.Id;
                b.Version = a.Version;
                var res = a.ToString() == b.ToString();
                Console.WriteLine(res ? "Same." : "Different");
                return res;
            }
        }

        /// <summary>
        /// The SetupAppBundleAsync.
        /// </summary>
        /// <returns>The <see cref="Task{string}"/>.</returns>
        private async Task<string> SetupAppBundleAsync()
        {
            Console.WriteLine("Setting up appbundle...");
            var myApp = $"{Owner}.{PackageName}+{Label}";
            var appResponse = await this.api.AppBundlesApi.GetAppBundleAsync(myApp, throwOnError: false);
            var app = new AppBundle()
            {
                Engine = TargetEngine,
                Id = PackageName
            };
            var package = CreateZip();
            if (appResponse.HttpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"\tCreating appbundle {myApp}...");
                await api.CreateAppBundleAsync(app, Label, package);
                return myApp;
            }
            await appResponse.HttpResponse.EnsureSuccessStatusCodeAsync();
            Console.WriteLine("\tFound existing appbundle...");
            if (!await EqualsAsync(package, appResponse.Content.Package))
            {
                Console.WriteLine($"\tUpdating appbundle {myApp}...");
                await api.UpdateAppBundleAsync(app, Label, package);
            }
            return myApp;

            async Task<bool> EqualsAsync(string a, string b)
            {
                Console.Write("\tComparing bundles...");
                using var aStream = File.OpenRead(a);
                var bLocal = await DownloadToDocsAsync(b, "das-appbundle.zip");
                using var bStream = File.OpenRead(bLocal);
                using var hasher = SHA256.Create();
                var res = hasher.ComputeHash(aStream).SequenceEqual(hasher.ComputeHash(bStream));
                Console.WriteLine(res ? "Same." : "Different");
                return res;
            }
        }

        /// <summary>
        /// The SetupOwnerAsync.
        /// </summary>
        /// <returns>The <see cref="Task{bool}"/>.</returns>
        private async Task<bool> SetupOwnerAsync()
        {
            Console.WriteLine("Setting up owner...");
            var nickname = await api.GetNicknameAsync("me");
            if (nickname == config.ClientId)
            {
                Console.WriteLine("\tNo nickname for this clientId yet. Attempting to create one...");
                HttpResponseMessage resp;
                resp = await api.ForgeAppsApi.CreateNicknameAsync("me", new NicknameRecord() { Nickname = Owner }, throwOnError: false);
                if (resp.StatusCode == HttpStatusCode.Conflict)
                {
                    Console.WriteLine("\tThere are already resources associated with this clientId or nickname is in use. Please use a different clientId or nickname.");
                    return false;
                }
                await resp.EnsureSuccessStatusCodeAsync();
            }
            return true;
        }

        /// <summary>
        /// The CreateZip.
        /// </summary>
        /// <returns>The <see cref="string"/>.</returns>
        static string CreateZip()
        {
            Console.WriteLine("\tGenerating autoloader zip...");
            string zip = Path.Combine(Directory.GetCurrentDirectory(), "package.zip");
            if (!File.Exists(zip))
                throw new FileNotFoundException("Missing package.zip!");
            return zip;
        }

        /// <summary>
        /// The DownloadToDocsAsync.
        /// </summary>
        /// <param name="url">The url<see cref="string"/>.</param>
        /// <param name="localFile">The localFile<see cref="string"/>.</param>
        /// <returns>The <see cref="Task{string}"/>.</returns>
        public async Task<string> DownloadToDocsAsync(string url, string localFile)
        {
            var report = FilePaths.OutPut;
            var fname = Path.Combine(report, localFile);
            if (File.Exists(fname))
                File.Delete(fname);
            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using (var fs = new FileStream(fname, FileMode.CreateNew))
            {
                await response.Content.CopyToAsync(fs);
            }

            return fname;
        }

        /// <summary>
        /// Defines the UrlType.
        /// </summary>
        public enum UrlType
        {
            /// <summary>
            /// Defines the downLoadUrl.
            /// </summary>
            downLoadUrl = 1,
            /// <summary>
            /// Defines the upLoadUrl.
            /// </summary>
            upLoadUrl = 2
        }

        /// <summary>
        /// The GetAzureUploadUrl.
        /// </summary>
        /// <param name="urlType">The urlType<see cref="UrlType"/>.</param>
        /// <param name="blobName">The blobName<see cref="string"/>.</param>
        /// <param name="filePath">The filePath<see cref="string"/>.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public async Task<string> GetAzureUrlAsync(UrlType urlType, string blobName, string filePath = "")
        {
            bool isReadOnly = true;
            string url = "";

            switch (urlType)
            {
                case UrlType.downLoadUrl:
                    {
                        isReadOnly = true;
                        break;
                    }

                case UrlType.upLoadUrl:
                    {
                        //We need to get Azure SAS write URL
                        isReadOnly = false;
                        break;
                    }

                default: break;
            }


            //Parse the connection string and return a reference to the storage account.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConfig.StorageConnectionString);
            //Create the blob client object.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            //Get a reference to a container to use for the sample code, and create it if it does not exist.
            CloudBlobContainer container = blobClient.GetContainerReference(AzureContainer);
            try
            {
                bool isCreated = container.CreateIfNotExists();
                if (isCreated)
                {
                    Console.WriteLine($"\n\t Container {AzureContainer} is created");
                }
                string sasBlobToken;

                //Get a reference to a blob within the container.
                //Note that the blob may not exist yet, but a SAS can still be created for it.
                CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
                if (File.Exists(filePath))
                {
                    //Upload input file from local disk to Azure blob
                    await blob.UploadFromFileAsync(filePath);
                }
                // Create a new access policy and define its constraints.
                // Note that the SharedAccessBlobPolicy class is used to define the parameters of an ad-hoc SAS, 
                SharedAccessBlobPolicy adHocSAS = new SharedAccessBlobPolicy()
                {
                    // Set start time to five minutes before now to avoid clock skew.
                    SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24),
                    Permissions = isReadOnly ? SharedAccessBlobPermissions.Read : SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.Create
                };

                //Generate the shared access signature on the blob, setting the constraints directly on the signature.
                sasBlobToken = blob.GetSharedAccessSignature(adHocSAS);
                url = blob.Uri + sasBlobToken;
                if (urlType.HasFlag(UrlType.downLoadUrl))
                {
                    DownloadUrl = url;
                    Console.WriteLine($"\tSuccess: signed resource for {blobName} created!\n\t{DownloadUrl.Mask()}");
                }
                else
                {
                    UploadUrl = url;
                    Console.WriteLine($"\tSuccess: signed resource for{blobName} created!\n\t{UploadUrl.Mask()}");
                }
            }
            catch (StorageException ex)
            {
                Console.WriteLine($"!!Error!! ->\n\t{ex.StackTrace}");
                throw;
            }
            return url;
        }
    }
}
