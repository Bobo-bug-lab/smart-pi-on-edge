// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using System;
using Azure;
using System.Net.Http;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Azure.Messaging.EventGrid;
using System.Text;

namespace FunctionDT
{
    public class IoTHubtoTwins
    {
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("IoTHubtoTwins")]
        // While async void should generally be used with caution, it's not uncommon for Azure function apps, since the function app isn't awaiting the task.
#pragma warning disable AZF0001 // Suppress async void error
        public async void Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
#pragma warning restore AZF0001 // Suppress async void error
        {
            if (adtInstanceUrl == null) log.LogError("Application setting \"ADT_SERVICE_URL\" not set");

            try
            {
                // Authenticate with Digital Twins
                var cred = new DefaultAzureCredential();
                var client = new DigitalTwinsClient(new Uri(adtInstanceUrl), cred);
                log.LogInformation($"ADT service client connection created.");

                if (eventGridEvent != null && eventGridEvent.Data != null)
                {
                    log.LogInformation(eventGridEvent.Data.ToString());

                    // <Find_device_ID_and_temperature>

                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
                    string bodyValue = deviceMessage["body"].ToString();
                    log.LogInformation($"bodyValue:{bodyValue}");
                    //string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];

                    byte[] bytes = Convert.FromBase64String(bodyValue);
                    string decodedString = Encoding.UTF8.GetString(bytes);
                    log.LogInformation($"decodedString:{decodedString}");
                    JObject decodedMessage = (JObject)JsonConvert.DeserializeObject(decodedString);

                    var room_number = decodedMessage["room"].Value<int>();
                    string roomId = $"Room{room_number}";
                    string lightId = $"Light0";


                    var temperature = decodedMessage["temperature"].Value<double>();
                    var humidity = decodedMessage["humidity"].Value<int>();
                    var detected = decodedMessage["detected"].Value<int>();

                    var light = decodedMessage["ledStatus"].Value<bool>();

                    // </Find_device_ID_and_temperature>

                    log.LogInformation($"Device:{roomId} Temperature is:{temperature}");
                    log.LogInformation($"Device:{roomId} humidity is:{humidity}");
                    log.LogInformation($"Device:{roomId} humidity is:{detected}");

                    // <Update_twin_with_device_temperature>
                    var updateRoomData = new JsonPatchDocument();
                    var updateLightData = new JsonPatchDocument();
                    updateRoomData.AppendReplace("/Temperature", temperature);
                    updateRoomData.AppendAdd("/humidity", humidity);
                    updateRoomData.AppendAdd("/detected", detected);

                    updateLightData.AppendReplace("/ledStatus", light);
                    await client.UpdateDigitalTwinAsync(roomId, updateRoomData);
                    await client.UpdateDigitalTwinAsync(lightId, updateLightData);
                    // </Update_twin_with_device_temperature>
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error in ingest function: {ex.Message}");
            }
        }
    }
}
