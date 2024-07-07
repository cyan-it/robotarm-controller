using System.Text;
using Gamepad;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;

Console.WriteLine("Hello, World!");

using var servoManager = new ServoManager();

using var baseServo = servoManager.ConnectServo(0);

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
    switch (button)
    {
        case GamepadButtons.Circle:
        case GamepadButtons.Triangle:
            if (!e.Pressed)
            {
                baseServo.EndMovement();
                break;
            }

            baseServo.BeginOrContinueMovement(0.5m, button == GamepadButtons.Circle ? ServoDirection.Right : ServoDirection.Left);
            break;
    }
};

gamepad.AxisChanged += (o, e) =>
{
    switch ((GamepadJoysticks?)e.Axis)
    {
        case GamepadJoysticks.LeftHorizontal:
            if (e.Value == 0)
            {
                baseServo.EndMovement();
                break;
            }

            decimal speed = Math.Abs(e.Value) / MaxAxisMovement;
            var direction = e.Value < 0 ? ServoDirection.Left : ServoDirection.Right;
            baseServo.BeginOrContinueMovement(speed, direction);
            break;
    }
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