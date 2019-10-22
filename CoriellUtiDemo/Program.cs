using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

/**
 * This program implements a simple commandline client for Coriell Life Sciences' API for
 * Women's Health, UTI, and Infectious Disease.
 *
 * Copyright 2019 Coriell Life Sciences, Inc.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
 * associated documentation files (the "Software"), to deal in the Software without restriction,
 * including without limitation the rights to use, copy, modify, merge, publish, distribute,
 * sublicense, and/or sell copies of the Software, and to permit persons to whom the Software
 * is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or
 * substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
 * PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
 * FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
 * OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */
namespace CoriellUtiDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var token = args.ElementAtOrDefault(0) ??
                        throw new ArgumentException("token is required as first argument");

            var client = ConfigureClient(token,
                Environment.GetEnvironmentVariable("CLS_BASEURL") ?? "https://api-dev.coriell-services.com");

            Repl(client);
        }

        static void Repl(HttpClient client)
        {
            bool keepGoing = true;
            while (keepGoing)
            {
                Console.WriteLine("next command: openarray | demo | report | quit");
                var cmd = Console.ReadLine();

                try
                {
                    switch (cmd.ToLowerInvariant())
                    {
                        case "openarray":
                        {
                            Console.WriteLine("Enter path to an OpenArray file");
                            var filepath = Console.ReadLine();
                            var response = UploadOpenArrayFile(client, filepath);
                            Console.WriteLine($"openarray upload response: {response}");
                        }
                            break;

                        case "demo":
                        {
                            Console.WriteLine("Enter path to an demographic (JSON) file");
                            var filepath = Console.ReadLine();
                            var demo =
                                JsonConvert.DeserializeObject<DemographicCreateRequest>(File.ReadAllText(filepath));
                            Console.WriteLine($"read: {demo}");
                            var response = PostDemographics(client, demo);
                            Console.WriteLine($"demographic response: {response}");
                        }
                            break;

                        case "q":
                        case "quit":
                            keepGoing = false;
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"exception: {e}");
                }
            }
        }

        static HttpClient ConfigureClient(string token, string baseAddress)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(baseAddress);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CLSDemo", "0.1"));

            return client;
        }


        /**
         * Upload an OpenArray file
         */
        static BatchUploadResult UploadOpenArrayFile(HttpClient client, string filepath)
        {
            if (!File.Exists(filepath))
            {
                throw new ArgumentException($"nonexistent file {filepath}");
            }

            using var stm = File.OpenRead(filepath);
            var content = new StreamContent(stm);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/tsv");

            var message = new HttpRequestMessage(HttpMethod.Post, "wh/loadStream")
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

            var message = new HttpRequestMessage(HttpMethod.Post, "wh/generateReport")
            {
                Content = content
            };

            var response = client.SendAsync(message).Result;
            var responseDetail = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"response is {response.StatusCode} => {responseDetail}");

            response.EnsureSuccessStatusCode();
        }


        /**
         * Post demographics
         */
        static DemographicSaveResponse PostDemographics(HttpClient client, DemographicCreateRequest request)
        {
            var ser = JsonConvert.SerializeObject(request, Formatting.None,
                new JsonSerializerSettings() {NullValueHandling = NullValueHandling.Ignore});
            Console.WriteLine($"serialized demographic as: {ser}");

            var content = new StringContent(ser);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            var message = new HttpRequestMessage(HttpMethod.Post, "wh/demo")
            {
                Content = content
            };

            var response = client.SendAsync(message).Result;
            var responseDetail = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine($"response is {response.StatusCode} => {responseDetail}");

            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<DemographicSaveResponse>(responseDetail);
        }
    }

    public class DemographicSaveResponse
    {
        public Guid id { get; set; }
        public override string ToString() => $"DemographicSaveResponse {id}";
    }

    public class DemographicCreateRequest
    {
        public string sampleName { get; set; }
        public Boolean? pregnant { get; set; }
        public Boolean? recurrentBv { get; set; }
        public Boolean? recurrentCandidia { get; set; }
        public Boolean? recurrentTrich { get; set; }
        public string sn { get; set; }
        public string givenName { get; set; }

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? dob { get; set; }

        public string sex { get; set; }
        public string physician { get; set; }
        public string npi { get; set; }
        public string practice { get; set; }
        public string physicianCity { get; set; }
        public string physicianState { get; set; }
        public string physicianPhone { get; set; }

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? collected { get; set; }

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? received { get; set; }

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? reported { get; set; }

        public string specimen { get; set; }
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
        public HashSet<string> sampleNames;

        public override string ToString() => $"batchId {batchId} ids {sampleNames.Count}";
    }

    public class DateOnlyConverter : DateTimeConverterBase
    {
        private const string Format = "yyyy-MM-dd";
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((DateTime) value).ToString(Format, CultureInfo.InvariantCulture));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (reader.Value == null)
            {
                return null;
            }

            var s = reader.Value.ToString();
            return DateTime.ParseExact(s, Format, CultureInfo.InvariantCulture);
        }
    }
}