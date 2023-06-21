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
    private MessageBodySend _mainMessageBody = new MessageBodySend();
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
        await _moduleClient.SetInputMessageHandlerAsync("inputFromCv", ProcessMessageFromCvAsync, _moduleClient);
        await _moduleClient.SetInputMessageHandlerAsync("inputFromNodered", ProcessMessageFromNoderedAsync, _moduleClient);
        
    }

    async Task<MessageResponse> ProcessMessageFromCvAsync(Message message, object userContext)
    {
        var counterValue = Interlocked.Increment(ref _counter);
        var reportedProperties = new TwinCollection();

        try
        {
            ModuleClient moduleClient = (ModuleClient)userContext;
            var messageBytes = message.GetBytes();
            var messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"CV Received message {counterValue}: [{messageString}]");

            // Get the message body.
            var messageBody = JsonConvert.DeserializeObject<MessageBodyCV>(messageString);

            if (messageBody != null)
            {
                Console.WriteLine($"CV Pic difference {messageBody.pic_diff} ");
                _pic_diff = messageBody.pic_diff;
                reportedProperties["Pic difference from CV"] = _pic_diff;              
            }

            // Indicate that the message treatment is completed.
            return MessageResponse.Completed;
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
                _mainMessageBody.light = messageBody.light;
                _mainMessageBody.detect = messageBody.detect;

                if(PIC_DIFF_THRESHOLD > _pic_diff && messageBody.detect==1)
                {
                    Console.WriteLine($"People detected");
                    if(messageBody.light < LIGHT_THRESHOLD)
                    {
                        led.TurnOn();
                    }
                }
                else    
                {
                    led.TurnOff();
                }
                _mainMessageBody.ledStatus = led.GetLED();
            }
            else
            {
                _mainMessageBody.temperature = messageBody.ambient.temperature;
                _mainMessageBody.humidity = messageBody.ambient.humidity;
                _mainMessageBody.timeCreated = messageBody.timeCreated;
            }

            _mainMessageBody.pic_diff = _pic_diff;
            _mainMessageBody.room = messageBody.room;
            // Create a response message
            
            var responseMessageJson = JsonConvert.SerializeObject(_mainMessageBody);
            var responseMessageBytes = Encoding.UTF8.GetBytes(responseMessageJson);
            var responseMessage = new Message(responseMessageBytes);
            reportedProperties["lastProcessedMessage"] = DateTime.UtcNow.ToString();
            reportedProperties["LED"] = led.GetLED();
            //reportedProperties["test_ceshi"] = "123"; working
            // Send the response message to "output"
            await moduleClient.SendEventAsync("output", responseMessage);
            await moduleClient.UpdateReportedPropertiesAsync(reportedProperties);

            // Indicate that the message treatment is completed.
            return MessageResponse.Completed;
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