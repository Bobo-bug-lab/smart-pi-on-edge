// File1.cs
using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using System;
using System.Text.Json;

public class DigitalTwinsService
{
    private static bool controlStatus;
    public static async Task<bool> LightCloudControl(int room, bool lightStatus)
    {
        string endpoint = "https://myTwins.api.weu.digitaltwins.azure.net";
        var credential = new DefaultAzureCredential();

        // DigitalTwinsClient 
        var client = new DigitalTwinsClient(new Uri(endpoint), credential);
        Console.WriteLine($"Service client created – ready to go");

        // set Twin ID
        string twinBuildingId = "MyBuilding";

        try
        {
            Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(twinBuildingId);

            var twin = twinResponse.Value;

            foreach (string prop in twin.Contents.Keys)
            {
                if (prop == "cloud_control" && twin.Contents.TryGetValue(prop, out object? value))
                {
                    Console.WriteLine($"Metadata property 'cloud_control': {value}");
                    //controlStatus = (bool)value;
                    controlStatus = ((JsonElement)value!).GetBoolean();
                }

                if (controlStatus)
                {
                    if (prop == "light_controller" && twin.Contents.TryGetValue(prop, out object? light))
                    {
                        Console.WriteLine("Cloud control on");
                        bool light_controller = ((JsonElement)light!).GetBoolean();
                        Console.WriteLine($"Metadata property 'light_controller': {light_controller}");

                        return light_controller;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read twin property: {ex.Message}");
        }
        
        return lightStatus;
    }
}
