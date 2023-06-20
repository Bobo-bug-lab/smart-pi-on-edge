public class MessageBody
{
    public Machine? machine {get;set;}
    public Ambient? ambient {get; set;}
    public string? timeCreated {get; set;}

    public float pic_diff{get; set;}


    //light_value, detect from esp->node;
    public int? light_value{get; set;}
    public int? detect{get; set;}
    public int? room{get; set;}

    

}
public class Machine
{
    public double? temperature {get; set;}
    public double? pressure {get; set;}
}
public class Ambient
{
    public double? temperature {get; set;}
    public int? humidity {get; set;}
}

public class VirtualLED
{
    private bool isOn;

    public VirtualLED()
    {
        isOn = false; // 默认初始状态为OFF
    }

    public void TurnOn()
    {
        isOn = true;
        Console.WriteLine("LED is turned ON");
    }

    public void TurnOff()
    {
        isOn = false;
        Console.WriteLine("LED is turned OFF");
    }

    public bool GetLED()
    {
        Console.WriteLine("LED status is: " + isOn);
        return isOn;
    }
}
