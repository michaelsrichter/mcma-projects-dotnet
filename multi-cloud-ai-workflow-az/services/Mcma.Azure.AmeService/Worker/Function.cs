using System.Threading.Tasks;
using Mcma.Azure.BlobStorage;
using Mcma.Azure.Client;
using Mcma.Azure.CosmosDb;
using Mcma.Azure.Functions.Logging;
using Mcma.Azure.Functions.Worker;
using Mcma.Client;
using Mcma.Core;
using Mcma.Core.Serialization;
using Mcma.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Mcma.Azure.AmeService.Worker
{
    public static class Function
    {
        static Function() => McmaTypes.Add<BlobStorageFileLocator>().Add<BlobStorageFolderLocator>();

        private static MicrosoftLoggerProvider LoggerProvider { get; } = new MicrosoftLoggerProvider("ame-service-worker");

        private static IAuthProvider AuthProvider { get; } = new AuthProvider().AddAzureAdManagedIdentityAuth();

        private static ProviderCollection ProviderCollection { get; } =
            new ProviderCollection(
                LoggerProvider,
                new ResourceManagerProvider(AuthProvider),
                new CosmosDbTableProvider(new CosmosDbTableProviderOptions().FromEnvironmentVariables()),
                AuthProvider
            );

        private static IWorker Worker =
            new Mcma.Worker.Worker(ProviderCollection)
                .AddJobProcessing<AmeJob>(op =>op.AddProfile<ExtractTechnicalMetadata>());
            
        [FunctionName("AmeServiceWorker")]
        public static async Task Run(
            [QueueTrigger("ame-service-work-queue", Connection = "WorkQueueStorage")] CloudQueueMessage queueMessage,
            ILogger log,
            ExecutionContext executionContext)
        {
            var workerRequest = queueMessage.ToWorkerRequest();

            LoggerProvider.AddLogger(log, workerRequest.Tracker);

            MediaInfoProcess.HostRootDir = executionContext.FunctionAppDirectory;

            await Worker.DoWorkAsync(workerRequest);
        }
    }
}
