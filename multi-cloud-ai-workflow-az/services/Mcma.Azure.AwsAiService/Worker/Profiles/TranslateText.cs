using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3.Model;
using Amazon.Translate;
using Amazon.Translate.Model;
using Mcma.Core;
using Mcma.Aws.S3;
using Mcma.Worker;
using Mcma.Azure.BlobStorage;
using Mcma.Azure.BlobStorage.Proxies;

namespace Mcma.Azure.AwsAiService.Worker
{
    internal class TranslateText : IJobProfile<AIJob>
    {
        public string Name => "AWS" + nameof(TranslateText);

        public async Task ExecuteAsync(ProcessJobAssignmentHelper<AIJob> jobHelper)
        {
            BlobStorageFileLocator inputFile;
            if (!jobHelper.JobInput.TryGet(nameof(inputFile), out inputFile))
                throw new Exception("Invalid or missing input file.");

            BlobStorageFolderLocator outputLocation;
            if (!jobHelper.JobInput.TryGet(nameof(outputLocation), out outputLocation))
                throw new Exception("Invalid or missing output location.");

            var inputText = await inputFile.Proxy(jobHelper.Request).GetAsTextAsync();
                
            TranslateTextResponse translateResponse;
            using (var translateService = new AmazonTranslateClient(jobHelper.Request.AwsCredentials(), jobHelper.Request.AwsRegion()))
                translateResponse =
                    await translateService.TranslateTextAsync(
                        new TranslateTextRequest
                        {
                            SourceLanguageCode = jobHelper.JobInput.TryGet("sourceLanguageCode", out string srcLanguageCode) ? srcLanguageCode : "auto",
                            TargetLanguageCode = jobHelper.JobInput.Get<string>("targetLanguageCode"),
                            Text = inputText
                        });

            // put results in file on Blob storage
            jobHelper.JobOutput["outputFile"] =
                await outputLocation.Proxy(jobHelper.Request).PutAsTextAsync("Translate-" + Guid.NewGuid().ToString() + ".txt", translateResponse.TranslatedText);

            await jobHelper.CompleteAsync();
        }
    }
}
