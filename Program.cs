using System.Text;
using Gamepad;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;

Console.WriteLine("Hello, World!");

using var servoManager = new ServoManager();

using var baseServo = servoManager.ConnectServo(0);
using var leftServo = servoManager.ConnectServo(1);
using var rightServo = servoManager.ConnectServo(2);
using var gripperServo = servoManager.ConnectServo(3);

using var gamepad = new GamepadController("/dev/input/js0");
// gamepad.ButtonChanged += (_, e) =>
// {
//     if (e.Button == 1 && e.Pressed)
//     {
//         motor3Start = Math.Max(10, motor3Start - 20);
//         servoMotor3.WriteAngle(motor3Start);
//     }
//     if (e.Button == 2 && e.Pressed)
//     {
//         motor3Start = Math.Min(170, motor3Start + 20);
//         servoMotor3.WriteAngle(motor3Start);
//     }
// };


var mqttFactory = new MqttFactory(new MqttConsoleLogger());
using var mqttClient = mqttFactory.CreateMqttClient();

var mqttClientOptions = new MqttClientOptionsBuilder()
    //.WithTcpServer("192.168.35.21", 1883)
    .WithTcpServer("dd0227a3-b4b3-4af6-add0-330c446d4400.k8s.civo.com", 30007)
    .WithClientId("RoboArm")
    .WithCredentials("MyUsername", "MyPassword")
    .Build();

mqttClient.ApplicationMessageReceivedAsync += e =>
{
    var angle = int.Parse(Encoding.ASCII.GetString(e.ApplicationMessage.PayloadSegment));
    var servo = Enum.Parse<Servos>(e.ApplicationMessage.Topic.Split('/').Last());

    switch(servo)
    {
        case Servos.Base:
            baseServo.QueueMoveTo(angle);
            break;
        case Servos.Left:
            leftServo.QueueMoveTo(angle);
            break;
        case Servos.Right:
            rightServo.QueueMoveTo(angle);
            break;
        case Servos.Gripper:
            gripperServo.QueueMoveTo(angle);
            break;
    }

    return Task.CompletedTask;
};

await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
    .WithTopicFilter("servo/+")
    .Build();

var sub = await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);


const decimal MaxAxisMovement = 32767;

gamepad.ButtonChanged += (o, e) =>
{
    var button = (GamepadButtons?)e.Button;
    
    if (!e.Pressed)
    {
        gripperServo.EndMovement();
        return;
    }

    gripperServo.BeginOrContinueMovement(0.5m, button == GamepadButtons.RightTop ? ServoDirection.Left : ServoDirection.Right);
};

gamepad.AxisChanged += (o, e) =>
{
    var value = e.Value;
    Servo servo;
    switch ((GamepadJoysticks?)e.Axis)
    {
        case GamepadJoysticks.LeftHorizontal:
            servo = baseServo;
            break;
        case GamepadJoysticks.LeftVertical:
            servo = leftServo;
            break;
        case GamepadJoysticks.RightVertical:
            servo = rightServo;
            value *= -1;
            break;
        default:
            return;
    }
    
    if (value == 0)
    {
        servo.EndMovement();
    }

    decimal speed = Math.Abs(value) / MaxAxisMovement;
    var direction = value < 0 ? ServoDirection.Left : ServoDirection.Right;
    servo.BeginOrContinueMovement(speed, direction);
};

Console.ReadLine();

await mqttClient.DisconnectAsync();



class MqttConsoleLogger() : IMqttNetLogger
{
    public bool IsEnabled => true;

    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[]? parameters, Exception? exception)
    {
        Console.WriteLine($"{logLevel}: {message}");
    }
}