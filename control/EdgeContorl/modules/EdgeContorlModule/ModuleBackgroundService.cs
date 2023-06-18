using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System.Text;
using System.Collections.Generic;     // For KeyValuePair<>
using Microsoft.Azure.Devices.Shared; // For TwinCollection
using Newtonsoft.Json;                // For JsonConvert
using System;
//using MessageModule;    //For definition of msgs

namespace EdgeContorlModule;


internal class ModuleBackgroundService : BackgroundService
{

    private int _counter;
    static double PIC_DIFF_THRESHOLD { get; set; } = 30;
    static int LIGHT_THRESHOLD { get; set; } = 10;
    float _pic_diff = 0;
    int detect = 0;
    private ModuleClient? _moduleClient;
    private CancellationToken _cancellationToken;
    private readonly ILogger<ModuleBackgroundService> _logger;
    private Message _outputMessage = new Message();

    public ModuleBackgroundService(ILogger<ModuleBackgroundService> logger) => _logger = logger;

    //  Creating a VirtualLED Object
    VirtualLED led = new VirtualLED();
    

    //get desired properties from twins
    static Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
    {
        try
        {
            Console.WriteLine("Desired property change:");
            Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

            if (desiredProperties["PIC_DIFF_THRESHOLD"]!=null)
                PIC_DIFF_THRESHOLD = desiredProperties["PIC_DIFF_THRESHOLD"];
            if (desiredProperties["LIGHT_THRESHOLD"]!=null)
                LIGHT_THRESHOLD = desiredProperties["LIGHT_THRESHOLD"];

        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", exception);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
        }
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        MqttTransportSettings mqttSetting = new(TransportType.Mqtt_Tcp_Only);
        ITransportSettings[] settings = { mqttSetting };

        // Open a connection to the Edge runtime
        _moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

        // Reconnect is not implented because we'll let docker restart the process when the connection is lost
        _moduleClient.SetConnectionStatusChangesHandler((status, reason) => 
            _logger.LogWarning("Connection changed: Status: {status} Reason: {reason}", status, reason));

        await _moduleClient.OpenAsync(cancellationToken);

        _logger.LogInformation("IoT Hub module client initialized.");

        var moduleTwin = await _moduleClient.GetTwinAsync();
        await OnDesiredPropertiesUpdate(moduleTwin.Properties.Desired, _moduleClient);

        // Attach a callback for updates to the module twin's desired properties.
        await _moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

        // Register callback to be called when a message is received by the module
        await _moduleClient.SetInputMessageHandlerAsync("inputFromNodered", ProcessMessageFromNoderedAsync, _moduleClient);
        await _moduleClient.SetInputMessageHandlerAsync("inputFromCv", ProcessMessageFromCvAsync, _moduleClient);
    }
    async Task<MessageResponse> ProcessMessageFromNoderedAsync(Message message, object userContext)
    {
        var counterValue = Interlocked.Increment(ref _counter);

        try
        {
            ModuleClient moduleClient = (ModuleClient)userContext;
            var reportedProperties = new TwinCollection();
            var messageBytes = message.GetBytes();
            var messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Nodered Received message {counterValue}: [{messageString}]");

            var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

            // Process the message here
            if (messageBody.light != null)
            {
                if(PIC_DIFF_THRESHOLD > _pic_diff && messageBody.detect==1)
                {
                    Console.WriteLine($"People detected");
                    if(messageBody.light < LIGHT_THRESHOLD)
                    {
                        led.TurnOn();
                    }
                }
                else    led.TurnOff();
            }
            // Create a response message
            var responseMessageString = "Processed message from Nodered";
            var responseMessageBytes = Encoding.UTF8.GetBytes(responseMessageString);
            var responseMessage = new Message(responseMessageBytes);
            reportedProperties["lastProcessedMessage"] = DateTime.UtcNow.ToString();
            reportedProperties["LED"] = led.GetLED();
            // Send the response message to "output"
            await moduleClient.SendEventAsync("output", responseMessage);
            await moduleClient.UpdateReportedPropertiesAsync(reportedProperties);

            // Indicate that the message treatment is completed.
            return MessageResponse.Completed;
        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", exception);
            }
            // Indicate that the message treatment is not completed.
            var moduleClient = (ModuleClient)userContext;
            return MessageResponse.Abandoned;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Error in sample: {0}", ex.Message);
            // Indicate that the message treatment is not completed.
            ModuleClient moduleClient = (ModuleClient)userContext;
            return MessageResponse.Abandoned;
        }
    }
    // async Task<MessageResponse> ProcessMessageFromNoderedAsync(Message message, object userContext)
    // {
    //     var counterValue = Interlocked.Increment(ref _counter);
    //     try
    //     {
    //         var moduleClient = (ModuleClient)userContext;
    //         var reportedProperties = new TwinCollection();
    //         var messageBytes = message.GetBytes();
    //         var messageString = Encoding.UTF8.GetString(messageBytes);
    //         Console.WriteLine($"Received message {counterValue}: [{messageString}]");

    //         // Deserialize the incoming message
    //         var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

    //         // Update the output message with new data
    //         if (messageBody != null)
    //         {
    //             _outputMessage.Properties.Add("Room",messageBody.room.ToString());
    //             _outputMessage.Properties.Add("Pic diff",_pic_diff.ToString());
    //             reportedProperties["pic_diff_value"] = messageBody.pic_diff;
    //             reportedProperties["Room"] = messageBody.room;
    //             // Combine the light and detect values from the first input
    //             if (messageBody.light != null)
    //             {
    //                 _outputMessage.Properties.Add("light_value",messageBody.light.ToString());
    //                 _outputMessage.Properties.Add("detect",messageBody.detect.ToString());
    //                 reportedProperties["light_value"] = messageBody.light;
    //                 reportedProperties["detect"] = messageBody.detect;
    //                 detect = messageBody.detect;
    //             }
    //             if (messageBody.ambient != null)
    //             {
    //                 _outputMessage.Properties["temperature"] = messageBody.ambient.temperature?.ToString();
    //                 _outputMessage.Properties["humidity"] = messageBody.ambient.humidity?.ToString();
    //                 _outputMessage.Properties["timeCreated"] = messageBody.timeCreated;
    //                 reportedProperties["temperature"] = messageBody.ambient.temperature;
    //                 reportedProperties["humidity"] = messageBody.ambient.humidity;
    //             }
    //             if(PIC_DIFF_THRESHOLD > _pic_diff && detect==1)
    //             {
    //                 Console.WriteLine($"People detected");
    //             }
    //             Console.WriteLine($"Updated output message: [{JsonConvert.SerializeObject(_outputMessage)}]");
    //         }

    //         reportedProperties["LED"] = led.GetLED();
    //         // Send the output message to the specified output channel
    //         await moduleClient.SendEventAsync("Room 0", _outputMessage);
    //         await moduleClient.UpdateReportedPropertiesAsync(reportedProperties);

    //         // Indicate that the message processing is completed
    //         return MessageResponse.Completed;
    //     }
    //     catch (AggregateException ex)
    //     {
    //         foreach (Exception exception in ex.InnerExceptions)
    //         {
    //             Console.WriteLine();
    //             Console.WriteLine("Error in sample: {0}", exception);
    //         }
    //         // Indicate that the message processing is not completed
    //         var moduleClient = (ModuleClient)userContext;
    //         return MessageResponse.Abandoned;
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine();
    //         Console.WriteLine("Error in sample: {0}", ex.Message);
    //         // Indicate that the message processing is not completed
    //         var moduleClient = (ModuleClient)userContext;
    //         return MessageResponse.Abandoned;
    //     }
    // }


    async Task<MessageResponse> ProcessMessageFromCvAsync(Message message, object userContext)
    {
        var counterValue = Interlocked.Increment(ref _counter);
        
        try
        {
            ModuleClient moduleClient = (ModuleClient)userContext;
            var messageBytes = message.GetBytes();
            var messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"CV Received message {counterValue}: [{messageString}]");

            // Get the message body.
            var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

            if (messageBody != null)
            {
                Console.WriteLine($"CV Pic difference {messageBody.pic_diff} ");
                _pic_diff = messageBody.pic_diff;              
            }

            // Indicate that the message treatment is completed.
            return MessageResponse.Completed;
        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", exception);
            }
            // Indicate that the message treatment is not completed.
            var moduleClient = (ModuleClient)userContext;
            return MessageResponse.Abandoned;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Error in sample: {0}", ex.Message);
            // Indicate that the message treatment is not completed.
            ModuleClient moduleClient = (ModuleClient)userContext;
            return MessageResponse.Abandoned;
        }

        }

}