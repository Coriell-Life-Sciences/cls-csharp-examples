using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;


namespace CoriellUtiDemo
{
    class Program
    {
        private const string Api = "https://api-dev.coriell-services.com";

        static void Main(string[] args)
        {
            string token = "d4ecf222-c8c9-49d8-b8fd-a327fa486581";
            string filepath = "/home/steve/projects/cls-engineering-misc/misc/wh/openarray/VAQ44_ReRuns_06_14_2016.txt";


            if (!File.Exists(filepath))
            {
                Console.WriteLine($"File {filepath} does not exist");
                Environment.ExitCode = 1;
            }
            else
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CLSDemo", "0.1"));

                var b = UploadOpenArrayFile(client, filepath);
                Console.WriteLine($"ok; {b}");
            }
        }

        /**
         * Upload an OpenArray file
         */
        static BatchUploadResult UploadOpenArrayFile(HttpClient client, string filepath)
        {
            var content = new StreamContent(File.OpenRead(filepath));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/tsv");

            var message = new HttpRequestMessage(HttpMethod.Post, $"{Api}/wh/loadStream")
            {
                Content = content,
                Headers =
                {
                    {"X-IgnoreUnmapped", "true"},
                    {"X-ChopTargets", "true"}
                }
            };

            Console.WriteLine($"sending {message.Content.Headers.ContentType}");

            var response = client.SendAsync(message).Result;
            var responseDetail = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"response is {response.StatusCode} => {responseDetail}");

            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<BatchUploadResult>(responseDetail);
        }

        /**
         * Generate an interpretation
         */
        static void CreateInterpretation(HttpClient client, Guid batchId, string sampleName,
            InterpretationOptions options)
        {
            var content = new StringContent(JsonConvert.SerializeObject(
                new
                {
                    batchId = batchId,
                    sampleName = sampleName,
                    options = options
                }));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            var message = new HttpRequestMessage(HttpMethod.Post, $"{Api}/wh/generateReport")
            {
                Content = content
            };
            
            var response = client.SendAsync(message).Result;
            var responseDetail = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"response is {response.StatusCode} => {responseDetail}");

            response.EnsureSuccessStatusCode();
        }
    }

    public class FlagSet
    {
        public object[] globalFlags;
        public object[] assayFlags;
    }
    
    public class InterpretationResponse
    {
        public Guid id { get; set; }
        public FlagSet flags { get; set; } 
    }
    
    public class InterpretationOptions
    {
        public int groupId { get; set; } = 2;
        public int qcGroupId { get; set; } = 1;
        public bool includeCharts { get; set; }
        public HashSet<string> panels { get; set; } = new HashSet<string>();
    }

    public class BatchUploadResult
    {
        public Guid batchId;
        public System.Collections.Generic.HashSet<string> sampleNames;

        public override string ToString() => $"batchId {batchId} ids {sampleNames.Count}";
    }
}